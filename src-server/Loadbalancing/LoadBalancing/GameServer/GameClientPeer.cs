// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GameClientPeer.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the GamePeer type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Photon.Common.Authentication;
using Photon.Hive.Plugin;
using Photon.LoadBalancing.Common;
using Photon.SocketServer.Diagnostics;

namespace Photon.LoadBalancing.GameServer
{
    #region using directives

    using System;

    using ExitGames.Logging;

    using Photon.Hive;
    using Photon.Hive.Caching;
    using Photon.Hive.Operations;
    using Photon.LoadBalancing.Operations;
    using Photon.SocketServer;

    using AuthSettings = Photon.Common.Authentication.Configuration.Auth.AuthSettings;
    using ErrorCode = Photon.Common.ErrorCode;
    using OperationCode = Photon.LoadBalancing.Operations.OperationCode;

    #endregion

    public class GameClientPeer : HivePeer
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        protected static readonly LogCountGuard canNotDecryptToken = new LogCountGuard(new TimeSpan(0, 1, 0));
        private static readonly LogCountGuard secureConnectionLogGuard = new LogCountGuard(new TimeSpan(0, 0, 30), 1);

        private readonly GameApplication application;

        private readonly long tokenExpirationTime = TimeSpan.FromSeconds(Settings.Default.AuthTokenExpirationS/3.0).Ticks;

        private readonly bool authOnceUsed;
        protected readonly bool binaryTokenUsed;

        #endregion

        #region Constructors and Destructors

        public GameClientPeer(InitRequest initRequest, GameApplication application)
            : this(initRequest, application, false)
        {
        }

        protected GameClientPeer(InitRequest initRequest, GameApplication application, bool derived)
            : base(initRequest)
        {
            this.application = application;

            if (this.application.AppStatsPublisher != null)
            {
                this.application.AppStatsPublisher.IncrementPeerCount();
            }

            this.HttpRpcCallsLimit = CommonSettings.Default.WebRpcHttpCallsLimit;

            var token = initRequest.InitObject as string;
            AuthenticationToken authToken = null;

            if (!string.IsNullOrEmpty(token))
            {
                ErrorCode errorCode;
                string errorMsg;
                authToken = AuthOnInitHandler.DoAuthUsingInitObject(token, this, initRequest,
                    application.TokenCreator, out errorCode, out errorMsg);
                if (authToken == null)
                {
                    this.RequestFiber.Enqueue(() => this.SendOperationResponse(new OperationResponse((byte) OperationCode.AuthOnce)
                    {
                        DebugMessage = errorMsg,
                        ReturnCode = (short) errorCode
                    }, new SendParameters()));
                    this.ScheduleDisconnect();
                }
            }
            else if (initRequest.DecryptedAuthToken != null)
            {
                authToken = (AuthenticationToken) initRequest.DecryptedAuthToken;
                this.binaryTokenUsed = true;
            }

            if (authToken != null)
            {
                this.authOnceUsed = true;
                this.AuthToken = authToken;

                if (!derived)
                {
                    if (!ConnectionRequirementsChecker.Check(this, authToken.ApplicationId, authToken, this.authOnceUsed))
                    {
                        log.Warn(secureConnectionLogGuard,
                            $"Client used non secure connection type when it is required. appId:{authToken.ApplicationId}, Connection: {this.NetworkProtocol}. AuthOnce");

                        return;
                    }

                    this.SetupPeer(this.AuthToken);

                    this.RequestFiber.Enqueue(() =>
                    {
                        var responseObject = new AuthenticateResponse { QueuePosition = 0 };
                        this.SendOperationResponse(new OperationResponse((byte)OperationCode.AuthOnce, responseObject),
                            new SendParameters());
                    });
                }
                
            }
        }

        #endregion

        #region Properties


        public DateTime LastActivity { get; protected set; }

        public byte LastOperation { get; protected set; }

        protected  bool IsAuthenticated { get; set; }

        #endregion

        #region Public Methods

        public override string ToString()
        {
            var roomName = string.Empty;
            var roomRef = this.RoomReference;
            if (roomRef != null)
            {
                var room = roomRef.Room;
                if (room != null)
                {
                    roomName = room.Name;
                }
            }

            return $"{this.GetType().Name}: " +
                   $"PID: {this.ConnectionId}, " +
                   $"IsConnected: {this.Connected}, " +
                   $"IsDisposed: {this.Disposed}, " +
                   $"Last Activity: Operation {this.LastOperation} at UTC {this.LastActivity:s}, " +
                   $"in Room '{roomName}', " +
                   $"IP {this.RemoteIP}:{this.RemotePort}, " +
                   $"NetworkProtocol {this.NetworkProtocol}, " +
                   $"JoinStage: {this.JoinStage}, " +
                   $"Last10:{this.GetLast10OpsAsString()}";
        }

        public override bool IsThisSameSession(HivePeer peer)
        {
            return this.AuthToken != null && peer.AuthToken != null && this.AuthToken.AreEqual(peer.AuthToken);
        }

        public override void UpdateSecure(string key, object value)
        {
            //always updated - keep this until behaviour is clarified
            if (this.AuthCookie == null)
            {
                this.AuthCookie = new Dictionary<string, object>();
            }
            this.AuthCookie[key] = value;
            this.SendAuthEvent();

            //we only update existing values
//            if (this.AuthCookie != null && this.AuthCookie.ContainsKey(key))
//            {
//                this.AuthCookie[key] = value;
//                this.SendAuthEvent();
//            }
        }

        #endregion

        #region Methods

        protected override void OnRoomNotFound(string gameId)
        {
            this.application.MasterServerConnection.RemoveGameState(gameId, GameRemoveReason.GameRemoveGameNotFound);
        }

        protected override void OnDisconnect(PhotonHostRuntimeInterfaces.DisconnectReason reasonCode, string reasonDetail)
        {
            base.OnDisconnect(reasonCode, reasonDetail);

            if (this.application.AppStatsPublisher != null)
            {
                this.application.AppStatsPublisher.DecrementPeerCount();
            }
        }

        protected override void OnOperationRequest(OperationRequest request, SendParameters sendParameters)
        {

            Dictionary<byte, object> dict = request.Parameters;
            foreach (object value in dict.Values)
            {
                log.Info("============GameClientPeer==========:" + value.ToString());
            }
            if (log.IsDebugEnabled)
            {
                if (request.OperationCode != (byte)Photon.Hive.Operations.OperationCode.RaiseEvent)
                {
                    log.DebugFormat("OnOperationRequest: conId={0}, opCode={1}", this.ConnectionId, request.OperationCode);
                }
            }

            this.LastActivity = DateTime.UtcNow;
            this.LastOperation = request.OperationCode;

            if (request.OperationCode == (byte) OperationCode.Authenticate)
            {
                if (this.IsAuthenticated)
                {
                    this.SendOperationResponse(new OperationResponse(request.OperationCode)
                    {
                        ReturnCode = (short) ErrorCode.OperationDenied,
                        DebugMessage = LBErrorMessages.AlreadyAuthenticated
                    }, sendParameters);
                    return;
                }

                this.HandleAuthenticateOperation(request, sendParameters);
                return;
            }

            if (!this.IsAuthenticated)
            {
                this.SendOperationResponse(new OperationResponse(request.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.OperationDenied,
                    DebugMessage = LBErrorMessages.NotAuthorized
                }, sendParameters);
                return;
            }

            base.OnOperationRequest(request, sendParameters);
        }

        protected override RoomReference GetOrCreateRoom(string gameId, params object[] args)
        {
            return this.application.GameCache.GetRoomReference(gameId, this, args);
        }

        protected override bool TryCreateRoom(string gameId, out RoomReference roomReference, params object[] args)
        {
            return this.application.GameCache.TryCreateRoom(gameId, this, out roomReference, args);
        }

        protected override bool TryGetRoomReference(string gameId, out RoomReference roomReference)
        {
            return this.application.GameCache.TryGetRoomReference(gameId, this, out roomReference);
        }

        protected override bool TryGetRoomWithoutReference(string gameId, out Room room)
        {
            return this.application.GameCache.TryGetRoomWithoutReference(gameId, out room); 
        }

        public virtual string GetRoomCacheDebugString(string gameId)
        {
            return this.application.GameCache.GetDebugString(gameId); 
        }

        protected virtual void HandleAuthenticateOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            var request = new AuthenticateRequest(this.Protocol, operationRequest);
            if (this.ValidateOperation(request, sendParameters) == false)
            {
                return;
            }

            this.AddOperationToQueue(operationRequest.OperationCode, $"IsTokenAuth:{request.IsTokenAuthUsed}");

            if (!request.IsTokenAuthUsed && !AuthSettings.Default.Enabled)
            {
                var authResponse = new OperationResponse(operationRequest.OperationCode)
                {
                    ReturnCode = (short) ErrorCode.InvalidAuthentication,
                    DebugMessage = ErrorMessages.InvalidAutenticationType
                };

                this.SendOperationResponse(authResponse, sendParameters);
                return;
            }

            var response = this.HandleAuthenticateTokenRequest(request);

            if (log.IsDebugEnabled)
            {
                log.DebugFormat(
                    "HandleAuthenticateRequest - Token Authentication done. Result: {0}; msg={1}",
                    response.ReturnCode,
                    response.DebugMessage);
            }

            this.SendOperationResponse(response, sendParameters);
        }

        protected void SetupPeer(string userId)
        {
            if (!string.IsNullOrEmpty(userId))
            {
                this.UserId = userId;
            }
            this.IsAuthenticated = true;
        }

        private void SetupPeer(AuthenticationToken authToken)
        {
            this.SetupPeer(authToken.UserId);
            this.AuthCookie = authToken.AuthCookie != null && authToken.AuthCookie.Count > 0 ? authToken.AuthCookie : null;
            this.AuthToken = authToken;
        }

        private OperationResponse HandleAuthenticateTokenRequest(AuthenticateRequest request)
        {
            OperationResponse response;

            var authToken = this.GetValidAuthToken(request, out response);
            if (response != null || authToken == null)
            {
                return response;
            }

            this.SetupPeer(authToken);
            // publish operation response
            var responseObject = new AuthenticateResponse { QueuePosition = 0 };
            return new OperationResponse(request.OperationRequest.OperationCode, responseObject);
        }

        private AuthenticationToken GetValidAuthToken(AuthenticateRequest authenticateRequest,
                                                      out OperationResponse operationResponse)
        {
            operationResponse = null;
            if (this.application.TokenCreator == null)
            {
                log.ErrorFormat("No custom authentication supported: AuthTokenKey not specified in config.");

                operationResponse = new OperationResponse(authenticateRequest.OperationRequest.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.InvalidAuthentication,
                    DebugMessage = ErrorMessages.AuthTokenTypeNotSupported
                };

                return null;
            }

            // validate the authentication token
            if (string.IsNullOrEmpty(authenticateRequest.Token))
            {
                operationResponse = new OperationResponse(authenticateRequest.OperationRequest.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.InvalidAuthentication,
                    DebugMessage = ErrorMessages.AuthTokenMissing
                };

                return null;
            }

            AuthenticationToken authToken;
            var tc = this.application.TokenCreator;
            string errorMsg;
            if (!tc.DecryptAuthenticationToken(authenticateRequest.Token, out authToken, out errorMsg))
            {
                log.WarnFormat(canNotDecryptToken, "Could not decrypt authenticaton token. errorMsg:{0}, token: {1}", errorMsg, authenticateRequest.Token);

                operationResponse = new OperationResponse(authenticateRequest.OperationRequest.OperationCode)
                {
                    ReturnCode = (short) ErrorCode.InvalidAuthentication,
                    DebugMessage = ErrorMessages.AuthTokenTypeNotSupported
                };

                return null;
            }

            if (authToken.ExpireAtTicks < DateTime.UtcNow.Ticks)
            {
                operationResponse = new OperationResponse(authenticateRequest.OperationRequest.OperationCode)
                {
                    ReturnCode = (short)Photon.Common.ErrorCode.AuthenticationTokenExpired,
                    DebugMessage = ErrorMessages.AuthTokenExpired
                };
                return null;
            }

            return authToken;
        }

        protected override PluginTraits GetPluginTraits()
        {
            return application.GameCache.PluginManager.PluginTraits;
        }

        protected override void HandleDebugGameOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (!GameServerSettings.Default.AllowDebugGameOperation)
            {
                this.SendOperationResponse(new OperationResponse(operationRequest.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.OperationDenied,
                    DebugMessage = LBErrorMessages.NotAuthorized
                }, sendParameters);
                return;
            }

            var debugRequest = new DebugGameRequest(this.Protocol, operationRequest);
            if (this.ValidateOperation(debugRequest, sendParameters) == false)
            {
                return;
            }

            string debug = string.Format("DebugGame called from PID {0}. {1}", this.ConnectionId, this.GetRoomCacheDebugString(debugRequest.GameId));
            operationRequest.Parameters.Add((byte)ParameterCode.Info, debug);


            if (this.RoomReference == null)
            {
                Room room;
                // get a room without obtaining a reference:
                if (!this.TryGetRoomWithoutReference(debugRequest.GameId, out room))
                {
                    var response = new OperationResponse
                    {
                        OperationCode = (byte)OperationCode.DebugGame,
                        ReturnCode = (short)ErrorCode.GameIdNotExists,
                        DebugMessage = HiveErrorMessages.GameIdDoesNotExist
                    };


                    this.SendOperationResponse(response, sendParameters);
                    return;
                }

                room.EnqueueOperation(this, debugRequest, sendParameters);
            }
            else
            {
                this.RoomReference.Room.EnqueueOperation(this, debugRequest, sendParameters);
            }
        }

        protected override void HandleRaiseEventOperation(OperationRequest operationRequest, SendParameters sendParameters)
        {
            base.HandleRaiseEventOperation(operationRequest, sendParameters);
            this.CheckAndUpdateTokenTtl();
        }

        private void CheckAndUpdateTokenTtl()
        {
            var utcNow = DateTime.UtcNow;
            if (this.AuthToken == null 
                || this.AuthToken.ExpireAtTicks - utcNow.Ticks > this.tokenExpirationTime
                || (this.AuthToken.IsFinalExpireAtUsed && this.AuthToken.FinalExpireAtTicks >= utcNow.Ticks))
            {
                return;
            }

            this.SendAuthEvent();
        }

        private void SendAuthEvent()
        {
            var response = new EventData((byte) LoadBalancing.Events.EventCode.AuthEvent)
            {
                Parameters = new Dictionary<byte, object>
                {
                    {(byte) ParameterCode.Token, this.GetEncryptedAuthToken()}
                }
            };
            this.SendEvent(response, new SendParameters());
        }

        protected virtual object GetEncryptedAuthToken()
        {
            var tc = this.application.TokenCreator;
            if (this.binaryTokenUsed)
            {
                return tc.EncryptAuthenticationTokenBinary(this.AuthToken, true);
            }
            return this.authOnceUsed ? tc.EncryptAuthenticationTokenV2(this.AuthToken, true)
                                    : tc.EncryptAuthenticationToken(this.AuthToken, true);
        }

        #endregion
    }
}