// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MasterClientPeer.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the MasterClientPeer type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
/*
 <AuthSettings Enabled="true" ClientAuthenticationAllowAnonymous="false">
    <AuthProviders>
      <AuthProvider
     Name="Custom"
        AuthenticationType="0"
        AuthUrl=""
    Key1="Val1"
    Key2="Val2"
        />
      <AuthProvider
     Name="Facebook"
        AuthenticationType="2"
        AuthUrl=""
    secret="Val1"
    appid="Val2"
        />
    </AuthProviders>
  </AuthSettings>
 * * 
 * */

using System.Collections.Generic;
using Photon.Common.Authentication;
using Photon.Common.Authentication.CustomAuthentication;
using Photon.Hive.WebRpc;
using Photon.LoadBalancing.Common;
using Photon.SocketServer.Diagnostics;

namespace Photon.LoadBalancing.MasterServer
{
    #region using directives

    using System;
    using System.Threading;
    using ExitGames.Logging;
    using Photon.LoadBalancing.Master.OperationHandler;
    using Photon.LoadBalancing.MasterServer.Lobby;
    using Photon.LoadBalancing.Operations;
    using Photon.SocketServer;
    using Photon.Hive.Common.Lobby;
    using Photon.Hive.Operations;
    using Photon.SocketServer.Rpc;

    using AppLobby = Photon.LoadBalancing.MasterServer.Lobby.AppLobby;
    using ErrorCode = Photon.Common.ErrorCode;
    using OperationCode = Photon.LoadBalancing.Operations.OperationCode;

    #endregion

    public class MasterClientPeer : Peer, ILobbyPeer, ICustomAuthPeer
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        protected static readonly LogCountGuard canNotDecryptToken = new LogCountGuard(new TimeSpan(0, 1, 0));
        protected static readonly LogCountGuard customAuthIsNotSetupLogGuard = new LogCountGuard(new TimeSpan(0, 0, 1));
        private static readonly LogCountGuard secureConnectionLogGuard = new LogCountGuard(new TimeSpan(0, 0, 30), 1);

        private GameApplication application;

        protected AuthenticationToken unencryptedAuthToken;

        private readonly TimeIntervalCounter httpForwardedRequests = new TimeIntervalCounter(new TimeSpan(0, 0, 1));

        private IGameListSubscription gameChannelSubscription;

        protected readonly bool authOnceUsed;
        protected readonly bool binaryTokenUsed;

        private IDisposable authTimeoutChecker;

        protected int concurrentJoinRequests;
        protected int totalSuccessfulJoinResponses;
        #endregion

        #region Constructors and Destructors

        public MasterClientPeer(InitRequest initRequest)
            : this(initRequest, false)
        {
            if (this.authOnceUsed)
            {
                this.SetCurrentOperationHandler(OperationHandlerDefault.Instance);
                var app = (MasterApplication)ApplicationBase.Instance;
                this.SetApplication(app.DefaultApplication);


                this.RequestFiber.Enqueue(() =>
                {
                    var response = new OperationResponse((byte)OperationCode.AuthOnce,
                        new AuthenticateResponse
                        {
                            QueuePosition = 0
                        }
                    );
                    response.Parameters.Add((byte)ParameterCode.Token, this.GetEncryptedAuthToken(app));
                    this.SendOperationResponse(response, new SendParameters());
                });
            }
        }

        protected MasterClientPeer(InitRequest initRequest, bool derived)
            : base(initRequest)
        {
            this.SetCurrentOperationHandler(OperationHandlerInitial.Instance);

            this.ExpectedProtocol = initRequest.NetworkProtocol;

            this.RequestFiber.Enqueue(() =>
                    {
                        if (MasterApplication.AppStats != null)
                        {
                            MasterApplication.AppStats.IncrementMasterPeerCount();
                            MasterApplication.AppStats.AddSubscriber(this);
                        }
                    }
                );

            this.HttpRpcCallsLimit = CommonSettings.Default.WebRpcHttpCallsLimit;

            AuthenticationToken authToken;
            if (initRequest.InitObject is string token)
            {
                ErrorCode errorCode;
                string errorMsg;
                authToken = AuthOnInitHandler.DoAuthUsingInitObject(token, this, initRequest, ((MasterApplication)MasterApplication.Instance).TokenCreator,
                    out errorCode, out errorMsg);

                if (authToken == null)
                {
                    this.RequestFiber.Enqueue(
                        () => this.SendOperationResponseAndDisconnect(new OperationResponse((byte)OperationCode.AuthOnce)
                        {
                            DebugMessage = errorMsg,
                            ReturnCode = (short)errorCode
                        }, new SendParameters()));
                    return;
                }
            }
            else if (initRequest.DecryptedAuthToken != null)
            {
                authToken = (AuthenticationToken)initRequest.DecryptedAuthToken;
                this.binaryTokenUsed = true;
            }
            else
            {
                this.StartWaitForAuthRequest();
                return;
            }

            if (authToken != null)
            {
                if (!derived && !ConnectionRequirementsChecker.Check(this, authToken.ApplicationId, authToken, true))
                {
                    log.Warn(secureConnectionLogGuard,
                        $"Client used non secure connection type when it is required. appId:{authToken.ApplicationId}, Connection: {this.NetworkProtocol}. AuthOnce");

                    return;
                }

                var app = (MasterApplication)ApplicationBase.Instance;
                if (app.DefaultApplication.IsActorExcluded(authToken.UserId))
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("MasterClientPeer, actor '{0}' is excluded", authToken.UserId);
                    }

                    this.RequestFiber.Enqueue(
                        () => this.SendOperationResponseAndDisconnect(new OperationResponse((byte)OperationCode.AuthOnce)
                        {
                            DebugMessage = "User blocked",
                            ReturnCode = (short)ErrorCode.UserBlocked,
                        }, new SendParameters()));
                    return;
                }

                this.authOnceUsed = true;
                this.unencryptedAuthToken = authToken;

                this.UserId = authToken.UserId;
            }

        }

        #endregion

        #region Properties

        public string UserId { get; set; }

        public bool UseHostnames
        {
            get
            {
                return this.IsIPv6ToIPv4Bridged;
            }
        }

        protected virtual GameApplication Application
        {
            get
            {
                return this.application;
            }

            set { this.SetApplication(value); }
        }

        private AppLobby AppLobby { get; set; }

        public IGameListSubscription GameChannelSubscription
        {
            set
            {
                if (value != null && !this.Connected)//if peer is disconnected already, subscription should be disposed
                {
                    value.Dispose();
                    value = null;
                }

                var old = Interlocked.Exchange(ref this.gameChannelSubscription, value);
                if (old != null)
                {
                    old.Dispose();
                }
            }
        }

        public WebRpcHandler WebRpcHandler { private get; set; }

        private int HttpRpcCallsLimit { get; set; }

        public NetworkProtocolType ExpectedProtocol { get; protected set; }

        NetworkProtocolType ILobbyPeer.NetworkProtocol
        {
            get { return this.ExpectedProtocol; }
        }

        internal Dictionary<string, object> AuthCookie { get { return this.unencryptedAuthToken?.AuthCookie; } }

        public bool AllowCreateJoinActivity
        {
            get
            {
                return this.concurrentJoinRequests <= MasterServerSettings.Default.MaxConcurrentJoinRequests
                    && this.totalSuccessfulJoinResponses <= MasterServerSettings.Default.MaxTotalJoinRequests;
            }
        }

        #endregion

        #region Methods

        public OperationResponse HandleAuthenticate(OperationRequest operationRequest, SendParameters sendParameters)
        {
            // validate operation request
            var authenticateRequest = new AuthenticateRequest(this.Protocol, operationRequest);
            if (authenticateRequest.IsValid == false)
            {
                return OperationHandlerBase.HandleInvalidOperation(authenticateRequest, log);
            }

            this.StopWaitForAuthRequest();

            if (log.IsDebugEnabled)
            {
                log.DebugFormat(
                    "HandleAuthenticateRequest:appId={0};version={1};region={2};type={3};userId={4}",
                    authenticateRequest.ApplicationId,
                    authenticateRequest.ApplicationVersion,
                    authenticateRequest.Region,
                    authenticateRequest.ClientAuthenticationType,
                    authenticateRequest.UserId);
            }

            var app = (MasterApplication)ApplicationBase.Instance;

            if (authenticateRequest.IsTokenAuthUsed)
            {
                OperationResponse response;

                var authToken = GetValidAuthToken(authenticateRequest, out response);
                if (response != null || authToken == null)
                {
                    return response;
                }

                if (app.DefaultApplication.IsActorExcluded(authToken.UserId))
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("HandleAuthenticate, userId '{0}' is excluded", authToken.UserId);
                    }

                    response = new OperationResponse((byte)OperationCode.Authenticate)
                    {
                        DebugMessage = "User blocked",
                        ReturnCode = (short)ErrorCode.UserBlocked
                    };
                    return response;
                }

                this.UserId = authToken.UserId;
                this.unencryptedAuthToken = authToken;

                if (!ConnectionRequirementsChecker.Check(this, authenticateRequest.ApplicationId,
                    this.unencryptedAuthToken, this.authOnceUsed))
                {
                    log.Warn(secureConnectionLogGuard,
                        $"Client used non secure connection type when it is required. appId:{authenticateRequest.ApplicationId}, Connection: {this.NetworkProtocol}, Reqular Authentication");

                    return null;
                }
                // publish operation response
                response = new OperationResponse(
                        authenticateRequest.OperationRequest.OperationCode,
                        new AuthenticateResponse
                        {
                            QueuePosition = 0
                        }
                    );
                response.Parameters.Add((byte)ParameterCode.Token, this.GetEncryptedAuthenticationToken(authenticateRequest));

                if (!string.IsNullOrEmpty(this.UserId))
                {
                    response.Parameters.Add((byte)ParameterCode.UserId, this.UserId);
                }

                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("HandleAuthenticateRequest - Token Authentication done. Result: {0}; msg={1}", response.ReturnCode, response.DebugMessage);
                }

                this.SetCurrentOperationHandler(OperationHandlerDefault.Instance);

                this.Application = app.DefaultApplication;

                // check if the peer wants to receive lobby statistic events
                if (authenticateRequest.ReceiveLobbyStatistics)
                {
                    this.Application.LobbyStatsPublisher.Subscribe(this);
                }

                return response;
            }


            // if authentication data is used it must be either a byte array or a string value
            if (authenticateRequest.ClientAuthenticationData != null)
            {
                var dataType = authenticateRequest.ClientAuthenticationData.GetType();
                if (dataType != typeof(byte[]) && dataType != typeof(string))
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("HandleAuthenticateRequest - invalid type for auth data (datatype = {0}), request: {1}", dataType, operationRequest.ToString());
                    }

                    return new OperationResponse
                    {
                        OperationCode = operationRequest.OperationCode,
                        ReturnCode = (short)ErrorCode.OperationInvalid,
                        DebugMessage = ErrorMessages.InvalidTypeForAuthData
                    };
                }
            }

            // check if custom client authentication is required
            if (app.CustomAuthHandler.IsClientAuthenticationEnabled)
            {
                if (app.TokenCreator == null)
                {
                    log.WarnFormat("No custom authentication supported: AuthTokenKey not specified in config.");

                    var response = new OperationResponse(authenticateRequest.OperationRequest.OperationCode)
                    {
                        ReturnCode = (short)ErrorCode.InvalidAuthentication,
                        DebugMessage = ErrorMessages.AuthTokenTypeNotSupported
                    };

                    return response;
                }


                this.SetCurrentOperationHandler(OperationHandlerAuthenticating.Instance);

                var authSettings = new AuthSettings
                {
                    IsAnonymousAccessAllowed = app.CustomAuthHandler.IsAnonymousAccessAllowed,
                };

                app.CustomAuthHandler.AuthenticateClient(this, authenticateRequest, authSettings, new SendParameters(), authSettings);
                return null;
            }

            var authResponse = new OperationResponse(operationRequest.OperationCode)
            {
                Parameters = new Dictionary<byte, object>()
            };

            // TBD: centralizing setting of userid
            if (string.IsNullOrWhiteSpace(authenticateRequest.UserId))
            {
                this.UserId = Guid.NewGuid().ToString();
                authResponse.Parameters.Add((byte)ParameterCode.UserId, this.UserId);
            }
            else
            {
                this.UserId = authenticateRequest.UserId;
            }

            authResponse.Parameters.Add((byte)ParameterCode.Token, this.GetEncryptedAuthenticationToken(authenticateRequest));

            // apply application to the peer
            this.SetCurrentOperationHandler(OperationHandlerDefault.Instance);

            this.OnAuthSuccess(authenticateRequest);

            // publish operation response
            return authResponse;
        }

        public virtual OperationResponse HandleJoinLobby(OperationRequest operationRequest, SendParameters sendParameters)
        {
            var joinLobbyRequest = new JoinLobbyRequest(this.Protocol, operationRequest);

            OperationResponse response;
            if (OperationHelper.ValidateOperation(joinLobbyRequest, log, out response) == false)
            {
                return response;
            }

            if (joinLobbyRequest.LobbyType > 3)
            {
                return new OperationResponse
                {
                    OperationCode = operationRequest.OperationCode,
                    ReturnCode = (short)ErrorCode.OperationInvalid,
                    DebugMessage = "Invalid lobby type " + joinLobbyRequest.LobbyType
                };
            }

            // remove peer from the currently joined lobby
            this.LeaveLobby();

            AppLobby lobby;
            string errorMsg;
            if (!this.Application.LobbyFactory.GetOrCreateAppLobby(joinLobbyRequest.LobbyName, (AppLobbyType)joinLobbyRequest.LobbyType, out lobby, out errorMsg))
            {
                // getting here should never happen
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("Could not get or create lobby: name={0}, type={1}", joinLobbyRequest.LobbyName, (AppLobbyType)joinLobbyRequest.LobbyType);
                }
                return new OperationResponse
                {
                    OperationCode = operationRequest.OperationCode,
                    ReturnCode = (short)ErrorCode.InternalServerError,
                    DebugMessage = errorMsg,
                };
            }

            this.AppLobby = lobby;
            this.AppLobby.JoinLobby(this, joinLobbyRequest, sendParameters);

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Joined lobby: {0}, {1}, u:'{2}'", joinLobbyRequest.LobbyName, joinLobbyRequest.LobbyType, this.UserId);
            }

            return null;
        }

        public virtual OperationResponse HandleLeaveLobby(OperationRequest operationRequest)
        {
            if (this.AppLobby == null)
            {
                return new OperationResponse
                {
                    OperationCode = operationRequest.OperationCode,
                    ReturnCode = (short)ErrorCode.Ok,
                    DebugMessage = LBErrorMessages.LobbyNotJoined
                };
            }

            this.LeaveLobby();

            return new OperationResponse(operationRequest.OperationCode);
        }

        public virtual OperationResponse HandleCreateGame(OperationRequest operationRequest, SendParameters sendParameters)
        {
            var response = this.CheckJoinActivity(operationRequest);
            if (response != null)
            {
                return response;
            }

            var createGameRequest = new CreateGameRequest(this.Protocol, operationRequest, this.UserId);

            if (OperationHelper.ValidateOperation(createGameRequest, log, out response) == false)
            {
                return response;
            }

            if (createGameRequest.LobbyType > 3)
            {
                return new OperationResponse
                {
                    OperationCode = operationRequest.OperationCode,
                    ReturnCode = (short)ErrorCode.OperationInvalid,
                    DebugMessage = "Invalid lobby type " + createGameRequest.LobbyType
                };
            }

            AppLobby lobby;
            response = this.TryGetLobby(createGameRequest.LobbyName, createGameRequest.LobbyType, operationRequest.OperationCode, out lobby);
            if (response != null)
            {
                return response;
            }

            this.IncConcurrentJoinRequest(1);
            lobby.EnqueueOperation(this, operationRequest, sendParameters);

            return null;
        }

        public virtual OperationResponse HandleFindFriends(OperationRequest operationRequest, SendParameters sendParameters)
        {
            // validate the operation request
            OperationResponse response;
            var operation = new FindFriendsRequest(this.Protocol, operationRequest);
            if (OperationHelper.ValidateOperation(operation, log, out response) == false)
            {
                return response;
            }

            // check if player online cache is available for the application
            var playerCache = this.Application.PlayerOnlineCache;
            if (playerCache == null)
            {
                return new OperationResponse((byte)OperationCode.FindFriends)
                {
                    ReturnCode = (short)ErrorCode.InternalServerError,
                    DebugMessage = "PlayerOnlineCache is not set!"
                };
            }

            playerCache.FiendFriends(this, operation, sendParameters);
            return null;
        }

        public virtual OperationResponse HandleJoinGame(OperationRequest operationRequest, SendParameters sendParameters)
        {
            var response = this.CheckJoinActivity(operationRequest);
            if (response != null)
            {
                return response;
            }

            var joinGameRequest = new JoinGameRequest(this.Protocol, operationRequest, this.UserId);

            if (OperationHelper.ValidateOperation(joinGameRequest, log, out response) == false)
            {
                return response;
            }

            GameState gameState;
            if (this.Application.TryGetGame(joinGameRequest.GameId, out gameState))
            {
                this.IncConcurrentJoinRequest(1);
                gameState.Lobby.EnqueueOperation(this, operationRequest, sendParameters);
                return null;
            }

            if (joinGameRequest.JoinMode == JoinModes.JoinOnly && !this.Application.PluginTraits.AllowAsyncJoin)
            {
                return new OperationResponse
                {
                    OperationCode = operationRequest.OperationCode,
                    ReturnCode = (short)ErrorCode.GameIdNotExists,
                    DebugMessage = HiveErrorMessages.GameIdDoesNotExist
                };
            }

            AppLobby lobby;
            response = this.TryGetLobby(joinGameRequest.LobbyName, joinGameRequest.LobbyType, operationRequest.OperationCode, out lobby);
            if (response != null)
            {
                return response;
            }

            this.IncConcurrentJoinRequest(1);
            lobby.EnqueueOperation(this, operationRequest, sendParameters);

            return null;
        }

        public virtual OperationResponse HandleJoinRandomGame(OperationRequest operationRequest, SendParameters sendParameters)
        {
            var response = this.CheckJoinActivity(operationRequest);
            if (response != null)
            {
                return response;
            }

            var joinRandomGameRequest = new JoinRandomGameRequest(this.Protocol, operationRequest);

            if (OperationHelper.ValidateOperation(joinRandomGameRequest, log, out response) == false)
            {
                return response;
            }

            AppLobby lobby;
            response = this.TryGetLobby(joinRandomGameRequest.LobbyName,
                joinRandomGameRequest.LobbyType, operationRequest.OperationCode, out lobby);
            if (response != null)
            {
                return response;
            }

            this.IncConcurrentJoinRequest(1);
            lobby.EnqueueOperation(this, operationRequest, sendParameters);

            return null;
        }

        public virtual OperationResponse HandleLobbyStatsRequest(OperationRequest operationRequest, SendParameters sendParameters)
        {

            if (log.IsDebugEnabled)
            {
                log.Debug($"Peer got lobby stats request. peer:{this}");
            }

            var getStatsRequest = new GetLobbyStatsRequest(this.Protocol, operationRequest);
            if (OperationHelper.ValidateOperation(getStatsRequest, log, out OperationResponse response) == false)
            {
                return response;
            }

            this.Application.LobbyStatsPublisher.EnqueueGetStatsRequest(this, getStatsRequest, sendParameters);
            return null;
        }

        public virtual OperationResponse HandleRpcRequest(OperationRequest operationRequest, SendParameters sendParameters)
        {
            if (this.WebRpcHandler != null)
            {
                if (this.HttpRpcCallsLimit > 0 && this.httpForwardedRequests.Increment(1) > this.HttpRpcCallsLimit)
                {
                    var resp = new OperationResponse
                    {
                        OperationCode = operationRequest.OperationCode,
                        ReturnCode = (short)ErrorCode.HttpLimitReached,
                        DebugMessage = HiveErrorMessages.HttpForwardedOperationsLimitReached
                    };

                    this.SendOperationResponse(resp, sendParameters);
                    return null;
                }

                this.WebRpcHandler.HandleCall(this, this.UserId, operationRequest, this.unencryptedAuthToken.AuthCookie, sendParameters);
                return null;
            }

            return new OperationResponse
            {
                OperationCode = operationRequest.OperationCode,
                ReturnCode = (short)ErrorCode.OperationDenied,
                DebugMessage = LBErrorMessages.RpcIsNotSetup
            };
        }

        public virtual OperationResponse HandleSettingsRequest(OperationRequest operationRequest, SendParameters sendParameters)
        {
            var request = new SettingsRequest(this.Protocol, operationRequest);
            if (!request.IsValid)
            {
                var msg = string.Format("HandleSettingsRequest error: {0}", request.GetErrorMessage());
                if (log.IsWarnEnabled)
                {
                    log.Warn(msg);
                }

                return new OperationResponse(operationRequest.OperationCode)
                {
                    DebugMessage = msg,
                    ReturnCode = (short)ErrorCode.OperationInvalid,
                };
            }

            if (request.ReceiveLobbyStatistics.HasValue)
            {
                // check if the peer wants to receive lobby statistic events
                if (request.ReceiveLobbyStatistics.Value)
                {
                    this.Application.LobbyStatsPublisher.Subscribe(this);
                }
                else
                {
                    this.Application.LobbyStatsPublisher.Unsubscribe(this);
                }
            }
            return null;
        }

        public virtual OperationResponse HandleGetGameList(OperationRequest operationRequest, SendParameters sendParameters)
        {
            var getGameListRequest = new GetGameListRequest(this.Protocol, operationRequest);

            OperationResponse response;
            if (OperationHelper.ValidateOperation(getGameListRequest, log, out response) == false)
            {
                return response;
            }

            //only supported for SqlListLobby
            if (getGameListRequest.LobbyType != (byte)AppLobbyType.SqlLobby)
            {
                return new OperationResponse
                {
                    OperationCode = operationRequest.OperationCode,
                    ReturnCode = (short)ErrorCode.OperationInvalid,
                    DebugMessage = "Invalid lobby type " + getGameListRequest.LobbyType
                };
            }
            //don't allow empty lobby name, this will cause that the default lobby is used (which does not support this operation)
            if (string.IsNullOrEmpty(getGameListRequest.LobbyName))
            {
                return new OperationResponse
                {
                    OperationCode = operationRequest.OperationCode,
                    ReturnCode = (short)ErrorCode.OperationInvalid,
                    DebugMessage = string.Format("Invalid lobby name: '{0}'", getGameListRequest.LobbyName)
                };
            }

            AppLobby lobby;
            response = this.TryGetLobby(getGameListRequest.LobbyName, getGameListRequest.LobbyType, operationRequest.OperationCode, out lobby);
            if (response != null)
            {
                return response;
            }

            lobby.EnqueueOperation(this, operationRequest, sendParameters);
            return null;
        }

        public void IncTotalSuccessfulJoinRequest()
        {
            Interlocked.Increment(ref this.totalSuccessfulJoinResponses);
        }

        public void IncConcurrentJoinRequest(int delta)
        {
            Interlocked.Add(ref this.concurrentJoinRequests, delta);
        }

        #region .privates

        private OperationResponse CheckJoinActivity(OperationRequest operationRequest)
        {
            if (!this.AllowCreateJoinActivity)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("HandleCreateGame was blocked. p:{0}", this);
                }

                if (log.IsWarnEnabled)
                {
                    log.WarnFormat(this.application.WrongJoinActivityGuard,
                        "Wrong Join activity. Peer has '{0}' concurrent join requests and '{1}' successful join response(s) in total. p:{2}",
                        this.concurrentJoinRequests, this.totalSuccessfulJoinResponses, this);
                }
                this.ScheduleDisconnect();
                return new OperationResponse(operationRequest.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.OperationDenied,
                    DebugMessage = HiveErrorMessages.OperationIsNotAllowedOnThisJoinStage,
                };
            }
            return null;
        }

        protected override void OnDisconnect(PhotonHostRuntimeInterfaces.DisconnectReason reasonCode, string reasonDetail)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Disconnect: peer={0}, UserId:{3}: reason={1}, detail={2}", this, reasonCode, reasonDetail, this.UserId);
            }

            // remove peer from the lobby if he has joined one
            this.LeaveLobby();

            // remove the peer from the application
            this.Application = null;

            // update application statistics
            if (MasterApplication.AppStats != null)
            {
                MasterApplication.AppStats.DecrementMasterPeerCount();
                MasterApplication.AppStats.RemoveSubscriber(this);
            }

            this.StopWaitForAuthRequest();
        }

        private static AuthenticationToken GetValidAuthToken(AuthenticateRequest authenticateRequest, out OperationResponse operationResponse)
        {
            operationResponse = null;
            var photonApplication = (MasterApplication)ApplicationBase.Instance;

            if (photonApplication.TokenCreator == null)
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
            var tokenCreator = photonApplication.TokenCreator;
            string errorMsg;
            if (!tokenCreator.DecryptAuthenticationToken(authenticateRequest.Token, out authToken, out errorMsg))
            {
                log.WarnFormat(canNotDecryptToken, "Could not decrypt authenticaton token. errorMsg:{0}, token: {1}", errorMsg, authenticateRequest.Token);

                operationResponse = new OperationResponse(authenticateRequest.OperationRequest.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.InvalidAuthentication,
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

        private OperationResponse TryGetLobby(string lobbyName, byte lobbyType, byte operationCode, out AppLobby lobby)
        {
            if (string.IsNullOrEmpty(lobbyName) && this.AppLobby != null)
            {
                lobby = this.AppLobby;
                return null;
            }

            string errorMsg;
            if (!this.Application.LobbyFactory.GetOrCreateAppLobby(lobbyName, (AppLobbyType)lobbyType, out lobby, out errorMsg))
            {
                // getting here should never happen
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("Could not get or create lobby: name={0}, type={1}. ErrorMsg:{2}", lobbyName, lobbyType, errorMsg);
                }

                return new OperationResponse
                {
                    OperationCode = operationCode,
                    ReturnCode = (short)ErrorCode.InternalServerError,
                    DebugMessage = errorMsg,
                };

            }

            return null;
        }

        protected void LeaveLobby()
        {
            if (this.AppLobby == null)
            {
                return;
            }

            this.GameChannelSubscription = null;
            this.AppLobby.LeaveLobby(this);
            this.AppLobby = null;
        }

        protected virtual object GetEncryptedAuthenticationToken(AuthenticateRequest request)
        {
            var app = (MasterApplication)ApplicationBase.Instance;

            if (this.unencryptedAuthToken == null)
            {
                this.unencryptedAuthToken = app.TokenCreator.CreateAuthenticationToken(this.UserId, request);
            }

            return this.GetEncryptedAuthToken(app);
        }

        protected object GetEncryptedAuthToken(MasterApplication app)
        {
            var tc = app.TokenCreator;
            if (this.binaryTokenUsed)
            {
                return tc.EncryptAuthenticationTokenBinary(this.unencryptedAuthToken, true);
            }
            return this.authOnceUsed ? tc.EncryptAuthenticationTokenV2(this.unencryptedAuthToken, true)
                : tc.EncryptAuthenticationToken(this.unencryptedAuthToken, true);
        }

        private void StartWaitForAuthRequest()
        {
            this.authTimeoutChecker = this.RequestFiber.Schedule(this.OnAuthRequestWaitFailure, MasterServerSettings.Default.DisconnectIfNoAuthInterval);
        }

        protected virtual void OnAuthRequestWaitFailure()
        {
            this.Disconnect();

            //int roundTripTime;
            //int rttVariance;
            //int numOfFailures;
            //this.GetStats(out roundTripTime, out rttVariance, out numOfFailures);
            //log.WarnFormat("Peer did not send valid auth request within one minute. p:{0}, LastActivity:{1}, rtt:{2}, rttv:{3}, numfailures:{4}, Protocol:{5}",
            //    this, this.GetLastTouch(), roundTripTime, rttVariance, numOfFailures, this.NetworkProtocol);
        }

        protected void StopWaitForAuthRequest()
        {
            if (this.authTimeoutChecker != null)
            {
                this.authTimeoutChecker.Dispose();
                this.authTimeoutChecker = null;
            }
            //if (MasterApplication.Instance.peerList != null && MasterApplication.Instance.peerList.Count > 0)
            //{
            //    MasterApplication.log.Info("=========客户端断开连接======");
            //    MasterApplication.Instance.peerList.Remove(this);
            //}

        }

        private void SetApplication(GameApplication value)
        {
            if (this.application == value)
            {
                return;
            }

            var oldApp = Interlocked.Exchange(ref this.application, value);
            if (oldApp != null)
            {
                oldApp.OnClientDisconnected(this);
            }

            if (value != null)
            {
                value.OnClientConnected(this);
            }
        }

        #endregion

        #region ICustomAuthPeer

        public virtual void OnCustomAuthenticationError(ErrorCode errorCode, string debugMessage, IAuthenticateRequest authenticateRequest, SendParameters sendParameters)
        {
            try
            {
                if (this.Connected == false)
                {
                    return;
                }

                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Client custom authentication failed: appId={0}, result={1}, msg={2}", authenticateRequest.ApplicationId, errorCode, debugMessage);
                }

                var operationResponse = new OperationResponse((byte)Hive.Operations.OperationCode.Authenticate)
                {
                    ReturnCode = (short)errorCode,
                    DebugMessage = debugMessage,
                };

                this.SendOperationResponse(operationResponse, sendParameters);
                this.SetCurrentOperationHandler(OperationHandlerInitial.Instance);
            }
            catch (Exception ex)
            {
                log.Error(ex);
                var errorResponse = new OperationResponse((byte)Hive.Operations.OperationCode.Authenticate) { ReturnCode = (short)Photon.Common.ErrorCode.InternalServerError };
                this.SendOperationResponse(errorResponse, sendParameters);
            }
        }

        public virtual void OnCustomAuthenticationResult(CustomAuthenticationResult customAuthResult, IAuthenticateRequest authenticateRequest,
            SendParameters sendParameters, object state)
        {
            var authRequest = (AuthenticateRequest)authenticateRequest;
            var authSettings = (AuthSettings)state;
            this.RequestFiber.Enqueue(() => this.DoCustomAuthenticationResult(customAuthResult, authRequest, sendParameters, authSettings));
        }

        #endregion

        #region CustomAuth Handling

        private void DoCustomAuthenticationResult(CustomAuthenticationResult customAuthResult,
            AuthenticateRequest authRequest, SendParameters sendParameters, AuthSettings customAuthSettings)
        {
            if (this.Connected == false)
            {
                return;
            }

            try
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Client custom authentication callback: result={0}, msg={1}, userId={2}",
                        customAuthResult.ResultCode,
                        customAuthResult.Message,
                        this.UserId);
                }

                var operationResponse = new OperationResponse((byte)Hive.Operations.OperationCode.Authenticate)
                {
                    DebugMessage = customAuthResult.Message,
                    Parameters = new Dictionary<byte, object>()
                };

                switch (customAuthResult.ResultCode)
                {
                    default:
                        operationResponse.ReturnCode = (short)Photon.Common.ErrorCode.CustomAuthenticationFailed;
                        this.SendOperationResponse(operationResponse, sendParameters);
                        this.SetCurrentOperationHandler(OperationHandlerInitial.Instance);
                        return;

                    case CustomAuthenticationResultCode.Data:
                        operationResponse.Parameters = new Dictionary<byte, object> { { (byte)ParameterCode.Data, customAuthResult.Data } };
                        this.SendOperationResponse(operationResponse, sendParameters);
                        this.SetCurrentOperationHandler(OperationHandlerInitial.Instance);
                        return;

                    case CustomAuthenticationResultCode.Ok:
                        //apply user id from custom auth result
                        if (!string.IsNullOrEmpty(customAuthResult.UserId))
                        {
                            this.UserId = customAuthResult.UserId;
                        }
                        else if (!string.IsNullOrEmpty(authRequest.UserId))
                        {
                            this.UserId = authRequest.UserId;
                        }
                        else
                        {
                            this.UserId = Guid.NewGuid().ToString();
                        }
                        // create auth token and send response
                        this.CreateAuthTokenAndSendResponse(customAuthResult, authRequest, sendParameters, customAuthSettings, operationResponse);
                        this.SetCurrentOperationHandler(OperationHandlerDefault.Instance);
                        this.OnAuthSuccess(authRequest);
                        break;
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                var errorResponse = new OperationResponse((byte)Hive.Operations.OperationCode.Authenticate)
                {
                    ReturnCode = (short)ErrorCode.InternalServerError,
                    DebugMessage = ex.Message
                };
                this.SendOperationResponse(errorResponse, sendParameters);
                this.SetCurrentOperationHandler(OperationHandlerInitial.Instance);
            }
        }

        protected virtual void CreateAuthTokenAndSendResponse(CustomAuthenticationResult customAuthResult, AuthenticateRequest authRequest,
            SendParameters sendParameters, AuthSettings authSettings, OperationResponse operationResponse)
        {
            var app = (MasterApplication)ApplicationBase.Instance;
            this.unencryptedAuthToken = app.TokenCreator.CreateAuthenticationToken(
                authRequest,
                authSettings,
                this.UserId,
                customAuthResult.AuthCookie);

            operationResponse.Parameters.Add((byte)ParameterCode.Token, this.GetEncryptedAuthenticationToken(authRequest));

            if (customAuthResult.Data != null)
            {
                operationResponse.Parameters.Add((byte)ParameterCode.Data, customAuthResult.Data);

            }
            if (customAuthResult.Nickname != null)
            {
                operationResponse.Parameters.Add((byte)ParameterCode.Nickname, customAuthResult.Nickname);
            }

            operationResponse.Parameters.Add((byte)ParameterCode.UserId, this.UserId);

            this.SendOperationResponse(operationResponse, sendParameters);
        }

        private void OnAuthSuccess(AuthenticateRequest request)
        {
            var app = (MasterApplication)ApplicationBase.Instance;
            this.Application = app.DefaultApplication;

            // check if the peer wants to receive lobby statistic events
            if (request.ReceiveLobbyStatistics)
            {
                this.Application.LobbyStatsPublisher.Subscribe(this);
            }
        }

        #endregion

        #endregion
    }
}