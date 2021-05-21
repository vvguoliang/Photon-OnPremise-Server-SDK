// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LitePeer.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Inheritance class of <see cref="PeerBase" />.
//   The LitePeer dispatches incoming <see cref="OperationRequest" />s at <see cref="OnOperationRequest">OnOperationRequest</see>.
//   When joining a <see cref="Room" /> a <see cref="Caching.RoomReference" /> is stored in the <see cref="RoomReference" /> property.
//   An <see cref="IFiber" /> guarantees that all outgoing messages (events/operations) are sent one after the other.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;
using Photon.Common;
using Photon.Common.Authentication;
using Photon.Hive.Caching;
using Photon.Hive.Messages;
using Photon.Hive.Operations;
using Photon.Hive.Plugin;
using Photon.Hive.WebRpc;
using Photon.SocketServer;
using Photon.SocketServer.Diagnostics;
using Photon.SocketServer.Rpc;
using PhotonHostRuntimeInterfaces;
using SendParameters = Photon.SocketServer.SendParameters;
using Settings = Photon.Common.Settings;

namespace Photon.Hive
{
    /// <summary>
    ///   Inheritance class of <see cref = "PeerBase" />.  
    ///   The LitePeer dispatches incoming <see cref = "OperationRequest" />s at <see cref = "OnOperationRequest">OnOperationRequest</see>.
    ///   When joining a <see cref = "Room" /> a <see cref = "Caching.RoomReference" /> is stored in the <see cref = "RoomReference" /> property.
    ///   An <see cref = "IFiber" /> guarantees that all outgoing messages (events/operations) are sent one after the other.
    /// </summary>
    public class HivePeer : ClientPeer
    {
        #region Constants and Fields

        /// <summary>
        ///   An <see cref = "ILogger" /> instance used to log messages to the logging framework.
        /// </summary>
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly LogCountGuard tokenValidationLogGuard = new LogCountGuard(new TimeSpan(0, 0, 10));

        private readonly TimeIntervalCounter httpForwardedRequests = new TimeIntervalCounter(new TimeSpan(0, 0, 1));

        /// <summary>
        /// we use it to create fake LeaveRequest if user's one  is brocken
        /// </summary>
        private static readonly OperationRequest EmptyLeaveRequest = new OperationRequest((byte)OperationCode.Leave)
        {
            Parameters = new Dictionary<byte, object>() { },
        };

        private int joinStage;

        private long roomCreationTS;
        private double peerCreationTime;

        class OpInfo
        {
            private readonly byte op;
            public string debugInfo;
            public double stamp;


            public OpInfo(byte op, string di, double ts)
            {
                this.op = op;
                this.debugInfo = di;
                this.stamp = ts;
            }

            public override string ToString()
            {
                return $"{this.op}, '{this.debugInfo}', {this.stamp} s";
            }
        }
        private readonly ConcurrentQueue<OpInfo> last10Operations = new ConcurrentQueue<OpInfo>();

        #endregion

        #region Constructors and Destructors

        /// <summary>
        ///   Initializes a new instance of the <see cref = "HivePeer" /> class.
        /// </summary>
        public HivePeer(InitRequest request)
            : base(request)
        {
            this.UserId = String.Empty;
            // we set here roomCreationTime to handle Auth request in a same way as others,
            // but we know that its ts is from peer creation
            this.roomCreationTS = Stopwatch.GetTimestamp();
            this.peerCreationTime = (double) this.roomCreationTS / Stopwatch.Frequency;
        }

        #endregion

        public static class JoinStages
        {
            public const byte Connected = 0;
            public const byte CreatingOrLoadingGame = 1;
            public const byte ConvertingParams = 2;
            public const byte CheckingCacheSlice = 3;
            public const byte AddingActor = 4;
            public const byte CheckAfterJoinParams = 5;
            public const byte ApplyActorProperties = 6;
            public const byte BeforeJoinComplete = 7;
            public const byte GettingUserResponse = 8;
            public const byte SendingUserResponse = 9;
            public const byte PublishingEvents = 10;
            public const byte EventsPublished = 11;
            public const byte Complete = 12;
        }

        #region Properties

        /// <summary>
        ///   Gets or sets a <see cref = "Caching.RoomReference" /> when joining a <see cref = "Room" />.
        /// </summary>
        protected RoomReference RoomReference { get; private set; }

        public Actor Actor { get; set; }
        public string UserId { get; protected set; }

        public WebRpcHandler WebRpcHandler { get; set; }

        public Dictionary<string, object> AuthCookie { get; protected set; }

        public AuthenticationToken AuthToken { get; protected set; }

        protected int HttpRpcCallsLimit { get; set; }

        /// <summary>
        /// The count of checks which were performed on this peer while it is in invalid state.
        /// </summary>
        public int CheckCount { get; set; }

        public int JoinStage { get { return this.joinStage; } }
        #endregion

        #region Public Methods

        /// <summary>
        ///   Checks if a operation is valid. If the operation is not valid
        ///   an operation response containing a desciptive error message
        ///   will be sent to the peer.
        /// </summary>
        /// <param name = "operation">
        ///   The operation.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        /// <returns>
        ///   true if the operation is valid; otherwise false.
        /// </returns>
        public bool ValidateOperation(Operation operation, SendParameters sendParameters)
        {
            if (operation.IsValid)
            {
                return true;
            }

            var errorMessage = operation.GetErrorMessage();
            this.SendOperationResponse(new OperationResponse
                                            {
                                                OperationCode = operation.OperationRequest.OperationCode, 
                                                ReturnCode = (short)ErrorCode.OperationInvalid, 
                                                DebugMessage = errorMessage
                                            }, 
                                            sendParameters);
            return false;
        }

        /// <summary>
        ///   Checks if the the state of peer is set to a reference of a room.
        ///   If a room refrence is present the peer will be removed from the related room and the reference will be disposed. 
        ///   Disposing the reference allows the associated room factory to remove the room instance if no more references to the room exists.
        /// </summary>
        public void RemovePeerFromCurrentRoom(int reason, string detail)
        {
            this.RequestFiber.Enqueue(() => this.RemovePeerFromCurrentRoomInternal(reason, detail));
        }

        public void ReleaseRoomReference()
        {
            this.RequestFiber.Enqueue(this.ReleaseRoomReferenceInternal);
        }

        public void OnJoinFailed(ErrorCode result, string details)
        {
            this.RequestFiber.Enqueue(() => this.OnJoinFailedInternal(result, details));
        }

        public virtual bool IsThisSameSession(HivePeer peer)
        {
            return false;
        }

        internal void SetJoinStage(byte stage)
        {
            Interlocked.Exchange(ref this.joinStage, stage);
        }

        public virtual void UpdateSecure(string key, object value)
        {
            //always updated - keep this until behaviour is clarified
            if (this.AuthCookie == null)
            {
                this.AuthCookie = new Dictionary<string, object>();
            }
            this.AuthCookie[key] = value;

            //we only update existing values
//            if (this.AuthCookie != null && this.AuthCookie.ContainsKey(key))
//            {
//                this.AuthCookie[key] = value;
//            }
        }

        public string GetLast10OpsAsString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"PeerTS: {this.peerCreationTime} s, RoomTS: {(double)this.roomCreationTS/Stopwatch.Frequency} s,");
            foreach (var opInfo in this.last10Operations)
            {
                sb.Append($"({opInfo}),");
            }

            return sb.ToString();
        }

        #endregion

        #region Methods

        protected virtual void HandleCreateGameOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (this.JoinStage != JoinStages.Connected || this.RoomReference != null)
            {
                this.OnWrongOperationStage(operationRequest, sendParameters);
                return;
            }

            // On "LoadBalancing" game servers games must by created first by the game creator to ensure that no other joining peer 
            // reaches the game server before the game is created.
            // we use JoinGameRequest to make sure that GameId is set
            var createRequest = new JoinGameRequest(this.Protocol, operationRequest, this.UserId);
            if (this.ValidateOperation(createRequest, sendParameters) == false)
            {
                return;
            }

            // try to create the game
            RoomReference gameReference;
            if (this.TryCreateRoom(createRequest.GameId, out gameReference) == false)
            {
                var response = new OperationResponse
                {
                    OperationCode = (byte)OperationCode.CreateGame,
                    ReturnCode = (short)ErrorCode.GameIdAlreadyExists,
                    DebugMessage = HiveErrorMessages.GameAlreadyExist,
                };

                this.SendOperationResponse(response, sendParameters);
                return;
            }

            // save the game reference in the peers state
            this.RoomReference = gameReference;

            this.roomCreationTS = gameReference.Room.RoomCreationTS;

            this.AddOperationToQueue(operationRequest.OperationCode);
            // finally enqueue the operation into game queue
            gameReference.Room.EnqueueOperation(this, createRequest, sendParameters);
        }

        /// <summary>
        ///   Enqueues RaiseEvent operation requests in the peers current game.
        /// </summary>
        /// <param name = "operationRequest">
        ///   The operation request.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        /// <remarks>
        ///   The current for a peer is stored in the peers state property. 
        ///   Using the <see cref = "Room.EnqueueOperation" /> method ensures that all operation request dispatch logic has thread safe access to all room instance members since they are processed in a serial order. 
        ///   <para>
        ///     Inheritors can use this method to enqueue there custom game operation to the peers current game.
        ///   </para>
        /// </remarks>
        protected virtual void HandleRaiseEventOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (this.joinStage < JoinStages.PublishingEvents)
            {
                this.OnWrongOperationStage(operationRequest, sendParameters);
                return;
            }

            var raiseEventOperation = new RaiseEventRequest(this.Protocol, operationRequest);

            if (this.ValidateOperation(raiseEventOperation, sendParameters) == false)
            {
                return;
            }

            this.AddOperationToQueue(operationRequest.OperationCode, "game op");


            // enqueue operation into game queue. 
            // the operation request will be processed in the games ExecuteOperation method.
            if (this.RoomReference != null)
            {
                this.RoomReference.Room.EnqueueOperation(this, raiseEventOperation, sendParameters);
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Received RaiseEvent operation on peer without a game: p:{0}", this);
            }
        }

        /// <summary>
        ///   Enqueues SetProperties operation requests in the peers current game.
        /// </summary>
        /// <param name = "operationRequest">
        ///   The operation request.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        /// <remarks>
        ///   The current for a peer is stored in the peers state property. 
        ///   Using the <see cref = "Room.EnqueueOperation" /> method ensures that all operation request dispatch logic has thread safe access to all room instance members since they are processed in a serial order. 
        ///   <para>
        ///     Inheritors can use this method to enqueue there custom game operation to the peers current game.
        ///   </para>
        /// </remarks>
        private void HandleSetPropertiesOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (this.joinStage < JoinStages.PublishingEvents)
            {
                this.OnWrongOperationStage(operationRequest, sendParameters);
                return;
            }

            var setPropertiesOperation = new SetPropertiesRequest(this.Protocol, operationRequest);
            if (this.ValidateOperation(setPropertiesOperation, sendParameters) == false)
            {
                return;
            }


            this.AddOperationToQueue(operationRequest.OperationCode, "game op");


            // enqueue operation into game queue. 
            // the operation request will be processed in the games ExecuteOperation method.
            if (this.RoomReference != null)
            {
                this.RoomReference.Room.EnqueueOperation(this, setPropertiesOperation, sendParameters);
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Received SetProperties operation on peer without a game: p:{0}", this);
            }
        }

        /// <summary>
        ///   Enqueues GetProperties operation requests in the peers current game.
        /// </summary>
        /// <param name = "operationRequest">
        ///   The operation request.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        /// <remarks>
        ///   The current for a peer is stored in the peers state property. 
        ///   Using the <see cref = "Room.EnqueueOperation" /> method ensures that all operation request dispatch logic has thread safe access to all room instance members since they are processed in a serial order. 
        ///   <para>
        ///     Inheritors can use this method to enqueue there custom game operation to the peers current game.
        ///   </para>
        /// </remarks>
        private void HandleGetPropertiesOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (this.joinStage < JoinStages.PublishingEvents)
            {
                this.OnWrongOperationStage(operationRequest, sendParameters);
                return;
            }

            var getPropertiesOperation = new GetPropertiesRequest(this.Protocol, operationRequest);
            if (this.ValidateOperation(getPropertiesOperation, sendParameters) == false)
            {
                return;
            }

            this.AddOperationToQueue(operationRequest.OperationCode, "game op");

            // enqueue operation into game queue. 
            // the operation request will be processed in the games ExecuteOperation method.
            if (this.RoomReference != null)
            {
                this.RoomReference.Room.EnqueueOperation(this, getPropertiesOperation, sendParameters);
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Received GetProperties operation on peer without a game: peerId={0}", this.ConnectionId);
            }
        }

        /// <summary>
        ///   Enqueues game related operation requests in the peers current game.
        /// </summary>
        /// <param name = "operationRequest">
        ///   The operation request.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        /// <remarks>
        ///   The current for a peer is stored in the peers state property. 
        ///   Using the <see cref = "Room.EnqueueOperation" /> method ensures that all operation request dispatch logic has thread safe access to all room instance members since they are processed in a serial order. 
        ///   <para>
        ///     Inheritors can use this method to enqueue there custom game operation to the peers current game.
        ///   </para>
        /// </remarks>
        private void HandleChangeGroupsOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (this.joinStage < JoinStages.PublishingEvents)
            {
                this.OnWrongOperationStage(operationRequest, sendParameters);
                return;
            }

            var changeGroupsOperation = new ChangeGroups(this.Protocol, operationRequest);
            if (this.ValidateOperation(changeGroupsOperation, sendParameters) == false)
            {
                return;
            }

            this.AddOperationToQueue(operationRequest.OperationCode, "game op");

            // enqueue operation into game queue. 
            // the operation request will be processed in the games ExecuteOperation method.
            if (this.RoomReference != null)
            {
                this.RoomReference.Room.EnqueueOperation(this, changeGroupsOperation, sendParameters);
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Received ChangeGroups operation on peer without a game: p:{0}", this);
            }
        }

        /// <summary>
        ///   Handles the <see cref = "JoinGameRequest" /> to enter a <see cref = "HiveGame" />.
        ///   This method removes the peer from any previously joined room, finds the room intended for join
        ///   and enqueues the operation for it to handle.
        /// </summary>
        /// <param name = "operationRequest">
        ///   The operation request to handle.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        protected virtual void HandleJoinGameOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (this.JoinStage != JoinStages.Connected || this.RoomReference != null)
            {
                this.OnWrongOperationStage(operationRequest, sendParameters);
                return;
            }

            // create join operation
            var joinRequest = new JoinGameRequest(this.Protocol, operationRequest, this.UserId);
            if (this.ValidateOperation(joinRequest, sendParameters) == false)
            {
                return;
            }

            // try to get the game reference from the game cache 
            RoomReference gameReference;
            var pluginTraits = this.GetPluginTraits();

            if (joinRequest.JoinMode > 0 || pluginTraits.AllowAsyncJoin)
            {
                gameReference = this.GetOrCreateRoom(joinRequest.GameId);
            }
            else
            {
                if (this.TryGetRoomReference(joinRequest.GameId, out gameReference) == false)
                {
                    this.HandleRoomNotFound(sendParameters, joinRequest);
                    return;
                }
            }

            // save the game reference in the peers state
            this.RoomReference = gameReference;
            this.roomCreationTS = gameReference.Room.RoomCreationTS;

            this.AddOperationToQueue(operationRequest.OperationCode, $"JoinMode:{joinRequest.JoinMode}");

            // finally enqueue the operation into game queue
            gameReference.Room.EnqueueOperation(this, joinRequest, sendParameters);
        }

        protected virtual PluginTraits GetPluginTraits()
        {
            return HiveGameCache.Instance.PluginManager.PluginTraits;
        }

        /// <summary>
        ///   Handles the <see cref = "LeaveRequest" /> to leave a <see cref = "HiveGame" />.
        /// </summary>
        /// <param name = "operationRequest">
        ///   The operation request to handle.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        protected virtual void HandleLeaveOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            // check if the peer have a reference to game 
            if (this.RoomReference == null)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Received leave operation on peer without a game: peerId={0}", this.ConnectionId);
                }

                return;
            }

            var leaveOperation = new LeaveRequest(this.Protocol, operationRequest);
            if (this.ValidateOperation(leaveOperation, sendParameters) == false)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"Wrong leave request. Use default one. errorMsg:{leaveOperation.GetErrorMessage()}");
                }
                // we create default request to remove actor for sure
                leaveOperation = new LeaveRequest(this.Protocol, EmptyLeaveRequest);
            }

            this.AddOperationToQueue(operationRequest.OperationCode, $"IsInActive:{leaveOperation.IsCommingBack}");

            var rr = this.RoomReference;
            this.RoomReference = null;
            // enqueue the leave operation into game queue. 
            rr.Room.EnqueueOperation(this, leaveOperation, sendParameters);

            DisposeRoomReference(rr);
        }

        /// <summary>
        ///   Handles a ping operation.
        /// </summary>
        /// <param name = "operationRequest">
        ///   The operation request to handle.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        protected virtual void HandlePingOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            this.AddOperationToQueue(operationRequest.OperationCode);

            this.SendOperationResponse(new OperationResponse { OperationCode = operationRequest.OperationCode }, sendParameters);
        }

        /// <summary>
        /// Handles WebRpc operation
        /// </summary>
        /// <param name = "request">
        ///   The operation request to handle.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        protected virtual void HandleRpcOperation(OperationRequest request, SendParameters sendParameters)
        {
            this.AddOperationToQueue(request.OperationCode);

            if (this.WebRpcHandler != null)
            {
                if (this.HttpRpcCallsLimit > 0 && this.httpForwardedRequests.Increment(1) > this.HttpRpcCallsLimit)
                {
                    var resp = new OperationResponse
                    {
                        OperationCode = request.OperationCode,
                        ReturnCode = (short)ErrorCode.HttpLimitReached,
                        DebugMessage = String.Format(HiveErrorMessages.HttpForwardedOperationsLimitReached, this.HttpRpcCallsLimit)
                    };

                    this.SendOperationResponse(resp, sendParameters);
                    return;
                }

                this.WebRpcHandler.HandleCall(this, this.UserId, request, this.AuthCookie, sendParameters);
                return;
            }

            this.SendOperationResponse(new OperationResponse
            {
                OperationCode = request.OperationCode,
                ReturnCode = (short)ErrorCode.OperationInvalid,
                DebugMessage = HiveErrorMessages.WebRpcIsNotEnabled,
            }, sendParameters);
        }


        /// <summary>
        ///   Called when client disconnects.
        ///   Ensures that disconnected players leave the game <see cref = "Room" />.
        ///   The player is not removed immediately but a message is sent to the room. This avoids
        ///   threading issues by making sure the player remove is not done concurrently with operations.
        /// </summary>
        protected override void OnDisconnect(DisconnectReason reasonCode, string reasonDetail)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("OnDisconnect: conId={0}, reason={1}, reasonDetail={2}", this.ConnectionId, reasonCode, reasonDetail);
            }

            this.AddOperationToQueue(0, $"reason:{reasonCode}, reasonDetail:{(reasonCode == DisconnectReason.TimeoutDisconnect ? string.Empty : reasonDetail)}");

            this.RemovePeerFromCurrentRoomInternal((int)reasonCode, reasonDetail);
        }

        /// <summary>
        ///   Called when the client sends an <see cref = "OperationRequest" />.
        /// </summary>
        /// <param name = "operationRequest">
        ///   The operation request.
        /// </param>
        /// <param name = "sendParameters">
        ///   The send Parameters.
        /// </param>
        protected override void OnOperationRequest(OperationRequest operationRequest, SendParameters sendParameters)
        {
            Dictionary<byte, object> dict = operationRequest.Parameters;
            foreach (object value in dict.Values)
            {
                log.Info("============HivePeer==========:" + value.ToString());
            }
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("OnOperationRequest. Code={0}", operationRequest.OperationCode);
            }

            var opCode = (OperationCode) operationRequest.OperationCode;
            switch (opCode)
            {
                case OperationCode.Authenticate:
                    this.AddOperationToQueue(operationRequest.OperationCode);
                    return;

                case OperationCode.CreateGame:
                    this.AddOperationToQueue(operationRequest.OperationCode);
                    this.HandleCreateGameOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.JoinGame:
                    this.HandleJoinGameOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.Ping:
                    this.HandlePingOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.DebugGame:
                    this.HandleDebugGameOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.Leave:
                    this.HandleLeaveOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.RaiseEvent:
                    this.HandleRaiseEventOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.GetProperties:
                    this.HandleGetPropertiesOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.SetProperties:
                    this.HandleSetPropertiesOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.ChangeGroups:
                    this.HandleChangeGroupsOperation(operationRequest, sendParameters);
                    return;

                case OperationCode.Rpc:
                    this.HandleRpcOperation(operationRequest, sendParameters);
                    return;

                default:
                    this.HandleUnknownOperationCode(operationRequest, sendParameters);
                    return;
            }
        }

        protected void HandleUnknownOperationCode(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Unknown operation code: OpCode={0}", operationRequest.OperationCode);
            }

            this.SendOperationResponse(
                new OperationResponse(operationRequest.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.OperationInvalid,
                    DebugMessage = HiveErrorMessages.UnknownOperationCode
                }, sendParameters);
        }

        protected virtual void HandleDebugGameOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
        }

        protected virtual RoomReference GetOrCreateRoom(string gameId, params object[] args)
        {
            return HiveGameCache.Instance.GetRoomReference(gameId, this, args);
        }

        protected virtual bool TryCreateRoom(string gameId, out RoomReference roomReference, params object[] args)
        {
            return HiveGameCache.Instance.TryCreateRoom(gameId, this, out roomReference, args);
        }

        protected virtual bool TryGetRoomReference(string gameId, out RoomReference roomReference)
        {
            return HiveGameCache.Instance.TryGetRoomReference(gameId, this, out roomReference);
        }

        protected virtual bool TryGetRoomWithoutReference(string gameId, out Room room)
        {
            return HiveGameCache.Instance.TryGetRoomWithoutReference(gameId, out room);
        }

        protected virtual void OnRoomNotFound(string gameId)
        {
        }

        private void OnJoinFailedInternal(ErrorCode result, string details)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("OnJoinFailedInternal: {0} - {1}", result, details);
            }

            // if join operation failed -> release the reference to the room
            if (result != ErrorCode.Ok && this.RoomReference != null)
            {
                this.ReleaseRoomReferenceInternal();
            }
        }

        private void ReleaseRoomReferenceInternal()
        {
            var r = this.RoomReference;
            if (DisposeRoomReference(r)) return;

            // finally the peers state is set to null to indicate
            // that the peer is not attached to a room anymore.
            this.RoomReference = null;
        }

        private static bool DisposeRoomReference(RoomReference r)
        {
            if (r == null)
            {
                return true;
            }

            // release the reference to the game
            // the game cache will recycle the game instance if no 
            // more refrences to the game are left.
            r.Dispose();
            return false;
        }

        private void RemovePeerFromCurrentRoomInternal(int reason, string detail)
        {
            // check if the peer already joined another game
            var r = this.RoomReference;
            if (r == null)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("RemovePeerFromCurrentRoom: Room Reference is null for p:{0}", this);
                }
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("RemovePeerFromCurrentRoom: Removing peer from room. p:{0}", this);
            }
            // remove peer from his current game.
            var message = new RoomMessage((byte)GameMessageCodes.RemovePeerFromGame, new object[] { this, reason, detail });
            r.Room.EnqueueMessage(message);

            this.ReleaseRoomReferenceInternal();
        }

        private void HandleRoomNotFound(SendParameters sendParameters, JoinGameRequest joinRequest)
        {
            this.OnRoomNotFound(joinRequest.GameId);

            var response = new OperationResponse
            {
                OperationCode = (byte)OperationCode.JoinGame,
                ReturnCode = (short)ErrorCode.GameIdNotExists,
                DebugMessage = HiveErrorMessages.GameIdDoesNotExist,
            };

            this.SendOperationResponse(response, sendParameters);

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Game '{0}' userId '{1}' failed to join. msg:{2} -- peer:{3}", joinRequest.GameId, this.UserId,
                    HiveErrorMessages.GameIdDoesNotExist, this);
            }
        }

        private void OnWrongOperationStage(OperationRequest operationRequest, SendParameters sendParameters)
        {
            this.SendOperationResponse(new OperationResponse
            {
                OperationCode = operationRequest.OperationCode,
                ReturnCode = (short)ErrorCode.OperationDenied,
                DebugMessage = HiveErrorMessages.OperationIsNotAllowedOnThisJoinStage,
            }, sendParameters);

        }

        protected void AddOperationToQueue(byte op, string stuff = "")
        {
            if (this.last10Operations.Count == 10)
            {
                this.last10Operations.TryDequeue(out var _);
            }

            this.last10Operations.Enqueue(new OpInfo(op, stuff, ((double)(Stopwatch.GetTimestamp() - this.roomCreationTS))/Stopwatch.Frequency));
        }
        #endregion
    }
}