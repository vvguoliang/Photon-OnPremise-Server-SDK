// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ClientPeer.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using Photon.Common.Authentication;
using Photon.Common.Authentication.CustomAuthentication;
using Photon.Common;
using Photon.SocketServer.Diagnostics;
using Photon.SocketServer.Rpc;
using Photon.SocketServer.Security;

namespace PhotonCloud.NameServer
{
    using System;
    using System.Collections.Generic;

    using Photon.SocketServer;

    using PhotonCloud.Authentication;

    using AuthenticateRequest = Photon.NameServer.Operations.AuthenticateRequest;
    using AuthenticateResponse = PhotonCloud.NameServer.Operations.AuthenticateResponse;
    using Photon.Common.Authentication.Data;
    using ExitGames.Logging;
    using ExitGames.Threading;
    using Photon.Cloud.Common.Diagnostic;
    using Photon.NameServer;
    using PhotonCloud.NameServer.Operations;

    public class ClientPeer : Photon.NameServer.ClientPeer
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly LogCountGuard xboxCustomAuthLogGuard = new LogCountGuard(new TimeSpan(0, 1, 0));

        private const string NullCluster = "Null";
        private const string NullStringToken = "Null";
        private const string NullUserId = "Null";

        private static readonly DateTime UnixStart = new DateTime(1970, 01, 01, 00, 00, 00, DateTimeKind.Utc);

        private static readonly Dictionary<byte, object> NullPayloadEncryptionData = new Dictionary<byte, object>
        {
            {0, (byte)0},
            {EncryptionDataParameters.EncryptionSecret, new byte[32]},
        };

        private static readonly Dictionary<byte, object> NullDatagrammEncryptionData = new Dictionary<byte, object>
        {
            {0, (byte)EncryptionModes.DatagramEncyption},
            {EncryptionDataParameters.EncryptionSecret, new byte[32]},
            {EncryptionDataParameters.AuthSecret, new byte[32]},
        };

        private static readonly Dictionary<byte, object> NullDatagrammWithRIEncryptionData = new Dictionary<byte, object>
        {
            {0, (byte)EncryptionModes.DatagramEncyptionWithRandomInitialNumbers},
            {EncryptionDataParameters.EncryptionSecret, new byte[32]},
            {EncryptionDataParameters.AuthSecret, new byte[32]},
        };

        private static readonly LogCountGuard masterNotFoundGuard = new LogCountGuard(new TimeSpan(0, 1, 0));
        private static readonly LogCountGuard masterNotFoundForProtocolGuard = new LogCountGuard(new TimeSpan(0, 1, 0));
        private static readonly LogCountGuard appCheckGuard = new LogCountGuard(new TimeSpan(0, 1, 0));
        private static readonly LogCountGuard customAuthGuard = new LogCountGuard(new TimeSpan(0, 1, 0));
        private static readonly LogCountGuard secureConnectionLogGuard = new LogCountGuard(new TimeSpan(0, 0, 2));

        private readonly BeforeAfterExecutor Executor;

        #region .ctor

        public ClientPeer(PhotonCloudApp application, InitRequest initRequest)
            : base(application, initRequest)
        {
            this.Executor = (BeforeAfterExecutor)initRequest.UserData;
        }

        #endregion

        #region Implemented Interfaces

        #region IAuthenticationPeer

        #endregion

        #endregion

        #region Methods

        private void InitExecutor(string appId, string appVersion)
        {
            if (this.Executor == null)
            {
                log.WarnFormat("Peers executor is null");
                return;
            }
            this.Executor.BeforeExecute = () =>
            {
                LogTagsSetup.AddAppIdTags(appId, appVersion);
                LogTagsSetup.AddPeerTags(this);
            };
            this.Executor.AfterExecute = () => log4net.ThreadContext.Properties.Clear();
        }

        protected override void OnOperationRequest(OperationRequest operationRequest, SendParameters sendParameters)
        {
            try
            {
                var operationCode = (OperationCode)operationRequest.OperationCode;
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("OnOperationRequest: opCode={0}", operationCode);
                }

                OperationResponse operationResponse = null;

                switch (operationCode)
                {
                    case OperationCode.AuthOnce:
                        operationResponse = this.HandleAuthOnceRequest(operationRequest, sendParameters);
                        break;

                    case OperationCode.Authenticate:
                        operationResponse = this.HandleAuthenticateRequest(operationRequest, sendParameters);
                        break;

                    case OperationCode.GetRegionList:
                        operationResponse = this.HandleGetRegionListRequest(operationRequest, sendParameters);
                        break;

                    case OperationCode.GetCloudType:
                        operationResponse = HandleGetCloudTypeRequest();
                        break;
                    default:
                        {
                            if (log.IsDebugEnabled)
                            {
                                log.DebugFormat("Unknown operation {0}. Client info: appId={1},protocol={2},remoteEndpoint={3}:{4}", operationCode, this.authenticatedApplicationId, this.NetworkProtocol, this.RemoteIP, this.RemotePort);
                                this.HandleUnknownOperationCode(operationRequest, sendParameters);
                            }
                        }
                        break;
                }

                if (operationResponse != null)
                {
                    this.SendOperationResponse(operationResponse, sendParameters);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                this.SendInternalErrorResponse(operationRequest, sendParameters, ex.Message);
            }
        }

        protected override OperationResponse HandleAuthenticateRequest(AuthenticateRequest authenticateRequest,
           SendParameters sendParameters, NetworkProtocolType endpointProtocol)
        {
            if (!authenticateRequest.IsValid)
            {
                this.HandleInvalidOperation(authenticateRequest, sendParameters);
                return null;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Got Auth Request:appId={0};version={1};region={2};type={3};userId={4}",
                    authenticateRequest.ApplicationId,
                    authenticateRequest.ApplicationVersion,
                    authenticateRequest.Region,
                    authenticateRequest.ClientAuthenticationType,
                    authenticateRequest.UserId);
            }

            if (!string.IsNullOrEmpty(this.authenticatedApplicationId))
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat(
                        "Authenticate called twice: already authenticated with appId={5}. Will handle new AuthRequest: appId={0};version={1};region={2};type={3};userId={4}",
                        authenticateRequest.ApplicationId,
                        authenticateRequest.ApplicationVersion,
                        authenticateRequest.Region,
                        authenticateRequest.ClientAuthenticationType,
                        authenticateRequest.UserId,
                        this.authenticatedApplicationId);
                }
            }
            // checking appId correctness
            Guid guid;
            if (!Guid.TryParse(authenticateRequest.ApplicationId, out guid))
            {
                if (log.IsInfoEnabled)
                {
                    var appId = authenticateRequest.ApplicationId ?? string.Empty;

                    log.InfoFormat("Authentication of client failed: Wrong applicationId, AppId={0}",
                        appId.Length > 50 ? appId.Substring(0, 50) + "..." : appId);
                }

                this.SendOperationResponse(new OperationResponse(authenticateRequest.OperationRequest.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.InvalidAuthentication,
                    DebugMessage = ErrorMessages.InvalidAppId
                }, sendParameters);

                this.ScheduleDisconnect(this.GetDisconnectTime());
                return null;
            }

            this.InitExecutor(authenticateRequest.ApplicationId, authenticateRequest.ApplicationVersion);
            ((PhotonCloudApp)this.application).AuthenticationCache.GetAccount(authenticateRequest.ApplicationId, this.RequestFiber,
                account => this.OnGetApplicationAccount(account, authenticateRequest, sendParameters, endpointProtocol));

            // authenticate application id
            return null;
        }

        private void OnGetApplicationAccount(ApplicationAccount account, AuthenticateRequest request, SendParameters sendParameters, NetworkProtocolType endpointProtocol)
        {
            if (!this.Connected)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("OnGetApplicationAccount: Client disconnected. Ignore response.");
                }
                return;
            }

            if (!ConnectionRequirementsChecker.Check(this, account.RequireSecureConnection, account.ApplicationId, this.authOnceUsed))
            {
                log.Warn(secureConnectionLogGuard,
                    $"Client used non secure connection type when it is required. appId:{account.ApplicationId}, Connection: {this.NetworkProtocol}. AuthOnce {this.authOnceUsed}");

                return;
            }


            if (log.IsDebugEnabled)
            {
                log.DebugFormat("OnGetApplicationAccount app:{0}, result:{1}, msg:{2}", account.ApplicationId,
                    account.AccountServiceResult, account.DebugMessage);
            }

            var operationRequest = request.OperationRequest;

            if (!account.IsAuthenticated)
            {
                if (log.IsInfoEnabled)
                {
                    log.InfoFormat("Authentication of client failed: Msg={0}, AppId={1}", account.DebugMessage, request.ApplicationId);
                }

                this.SendOperationResponse(new OperationResponse(operationRequest.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.InvalidAuthentication,
                    DebugMessage =
                        string.IsNullOrEmpty(account.DebugMessage) ? ErrorMessages.InvalidAppId : account.DebugMessage
                }, sendParameters);

                this.ScheduleDisconnect(this.GetDisconnectTime());
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat(
                    "HandleAuthenticateRequest for App ID {0}, Private Cloud {1}, Service Type {2}, Region {3}",
                    request.ApplicationId,
                    account.PrivateCloud,
                    account.ServiceType,
                    request.Region);
            }

            // store for debugging purposes. 
            this.authenticatedApplicationId = request.ApplicationId;

            // try to get the master server instance for the specified application id
            CloudPhotonEndpointInfo masterServer;
            string message;

            if (!((PhotonCloudApp)this.application).CloudCache.TryGetPhotonEndpoint(request, account, out masterServer, out message))
            {
                if (string.Equals(request.Region, "none", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("MasterServer not found for region '{0}' on cloud '{1}' / service type '{2}': '{3}'. AppId: {4}/{5}",
                            request.Region, account.PrivateCloud, account.ServiceType, message,
                            request.ApplicationId, request.ApplicationVersion);
                    }
                }
                else
                {
                    if (log.IsWarnEnabled)
                    {
                        log.WarnFormat(masterNotFoundGuard, "MasterServer not found for region '{0}' on cloud '{1}' / service type '{2}': '{3}'. AppId: {4}/{5}",
                            request.Region, account.PrivateCloud, account.ServiceType, message,
                            request.ApplicationId, request.ApplicationVersion);
                    }
                }
                this.SendOperationResponse(new OperationResponse(operationRequest.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.InvalidRegion,
                    DebugMessage = string.Format("Cloud {0} / Region {1} is not available.", account.PrivateCloud, request.Region)
                }, sendParameters);

                this.ScheduleDisconnect(this.GetDisconnectTime());
                return;
            }

            var masterEndPoint = masterServer.GetEndPoint(endpointProtocol, this.LocalPort,
                isIPv6: this.LocalIPAddressIsIPv6, useHostnames: this.IsIPv6ToIPv4Bridged);
            if (masterEndPoint == null)
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat(masterNotFoundForProtocolGuard,
                        "Master server endpoint for protocol {0} not found on master server {1}. appId:{2}",
                        this.NetworkProtocol, masterServer, account.ApplicationId);
                }

                this.SendOperationResponse(new OperationResponse(operationRequest.OperationCode)
                {
                    ReturnCode = (short)AuthErrorCode.ProtocolNotSupported,
                    DebugMessage = ErrorMessages.ProtocolNotSupported
                }, sendParameters);

                this.ScheduleDisconnect(this.GetDisconnectTime());
                return;
            }

            // check for custom authentication
            if (account.IsClientAuthenticationEnabled)
            {
                var customAuthHandler = this.GetCustomAuthHandler(account);
                customAuthHandler.AuthenticateClient(this, request, account, sendParameters, account);

                return; // custom authentication is handled async
            }

            var response = this.HandleDefaultAuthenticateRequest(request, account, masterEndPoint, masterServer);
            this.SendOperationResponse(response, sendParameters);
            this.ScheduleDisconnect(this.MaxDisconnectTime);
        }

        private OperationResponse HandleDefaultAuthenticateRequest(AuthenticateRequest authenticateRequest,
            ApplicationAccount applicationAccount, string masterEndPoint, CloudPhotonEndpointInfo masterServer)
        {
            // generate a userid if its not set by the client
            var userId = string.IsNullOrEmpty(authenticateRequest.UserId) ? Guid.NewGuid().ToString() : authenticateRequest.UserId;
            // create auth token
            var unencryptedToken = this.application.TokenCreator.CreateAuthenticationToken(authenticateRequest, applicationAccount,
                userId, new Dictionary<string, object>());

            var authToken = this.GetEncryptedAuthToken(unencryptedToken, masterServer);

            this.CheckEncryptedToken(authToken, authenticateRequest, applicationAccount, masterServer);

            var authResponse = new AuthenticateResponse
            {
                MasterEndpoint = masterEndPoint,
                AuthenticationToken = authToken,
                UserId = userId,
                Cluster = masterServer.Cluster,
                EncryptionData = GetEncryptionData(unencryptedToken),
            };

            return new OperationResponse(authenticateRequest.OperationRequest.OperationCode, authResponse);
        }

        private void CheckEncryptedToken(object authToken, IAuthenticateRequest authenticateRequest, ApplicationAccount applicationAccount, CloudPhotonEndpointInfo master)
        {
            VAppsAuthTokenFactory.CheckEncryptedToken(this.application.TokenCreator, appCheckGuard, authToken,
                        authenticateRequest, applicationAccount, master.UseV1Token);
        }

        private OperationResponse HandleGetRegionListRequest(OperationRequest operationRequest, SendParameters sendParameters)
        {
            var regionListRequest = new Photon.NameServer.Operations.GetRegionListRequest(this.Protocol, operationRequest);
            //appid check since field is not required for standalone NS
            if (!regionListRequest.IsValid || !regionListRequest.ValidateApplicationId())
            {
                this.HandleInvalidOperation(regionListRequest, sendParameters);
                return null;
            }

            // authenticate application id
            ((PhotonCloudApp)this.application).AuthenticationCache.GetAccount(regionListRequest.ApplicationId, this.RequestFiber,
                account => this.OnGetApplicationAccountToGetRegionList(account, regionListRequest, sendParameters));

            return null;
        }

        private void OnGetApplicationAccountToGetRegionList(ApplicationAccount appAccount, Photon.NameServer.Operations.GetRegionListRequest regionListRequest, SendParameters sendParameters)
        {
            if (!this.Connected)
            {
                return;
            }

            if (!appAccount.IsAuthenticated)
            {
                this.SendOperationResponse(new OperationResponse(regionListRequest.OperationRequest.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.InvalidAuthentication,
                    DebugMessage = string.IsNullOrEmpty(appAccount.DebugMessage) ? ErrorMessages.InvalidAppId : appAccount.DebugMessage
                }, sendParameters);

                this.ScheduleDisconnect(this.GetDisconnectTime());
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("GetRegionList for App ID {0}, Private Cloud {1}, Service Type {2}", regionListRequest.ApplicationId,
                    appAccount.PrivateCloud, appAccount.ServiceType);
            }

            List<string> regions;
            List<string> endPoints;
            string message;

            if (!((PhotonCloudApp)this.application).CloudCache.GetRegionList(regionListRequest, appAccount,
                this.NetworkProtocol, this.LocalPort, this.LocalIPAddressIsIPv6,
                this.IsIPv6ToIPv4Bridged, out regions, out endPoints, out message))
            {
                {
                    this.SendOperationResponse(new OperationResponse((byte)OperationCode.GetRegionList)
                    {
                        ReturnCode = (short)ErrorCode.InvalidRegion,
                        DebugMessage = message
                    }, sendParameters);

                    this.ScheduleDisconnect(this.GetDisconnectTime());
                    return;
                }
            }

            var regionListResponse = new Photon.NameServer.Operations.GetRegionListResponse
            {
                Endpoints = endPoints.ToArray(),
                Region = regions.ToArray()
            };

            this.SendOperationResponse(new OperationResponse((byte)OperationCode.GetRegionList, regionListResponse), sendParameters);
        }

        private static OperationResponse HandleGetCloudTypeRequest()
        {
            var response = new GetCloudTypeResponse
            {
                CloudType = Settings.Default.CloudType,
            };

            return new OperationResponse((byte)OperationCode.GetCloudType, response);
        }

        protected override void DoCustomAuthenticationError(ErrorCode errorCode, string debugMessage, IAuthenticateRequest authenticateRequest, SendParameters sendParameters)
        {
            var opReq = (Operation)authenticateRequest;
            try
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat(
                        "DoCustomAuthenticationError: appId={0}, errorCode={1}, debugMessage={2}", authenticateRequest.ApplicationId, errorCode, debugMessage);
                }

                if (!this.Connected)
                {
                    return;
                }

                if (errorCode == ErrorCode.CustomAuthenticationOverload)
                {
                    this.RedirectPeerToNull(authenticateRequest, sendParameters);
                }
                else
                {
                    var errorResponse = new OperationResponse(opReq.OperationRequest.OperationCode) { ReturnCode = (short)errorCode, DebugMessage = debugMessage };
                    this.SendOperationResponse(errorResponse, sendParameters);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);

                var errorResponse = new OperationResponse(opReq.OperationRequest.OperationCode) { ReturnCode = (short)ErrorCode.InternalServerError };
                this.SendOperationResponse(errorResponse, sendParameters);
            }

            this.ScheduleDisconnect(this.GetDisconnectTime());
        }

        private void RedirectPeerToNull(IAuthenticateRequest authRequest, SendParameters sendParameters)
        {
            var encryptionData = NullPayloadEncryptionData;
            if (this.authOnceUsed)
            {
                var authOnceRequest = (IAuthOnceRequest)authRequest;
                if (authOnceRequest.EncryptionMode == (byte)EncryptionModes.DatagramEncyption)
                {
                    encryptionData = NullDatagrammEncryptionData;
                }
                else
                {
                    encryptionData = NullDatagrammWithRIEncryptionData;
                }
            }

            var masterEndPoint = this.LocalIPAddressIsIPv6 ? Settings.Default.IPv6NullAddress : Settings.Default.IPv4NullAddress;
            masterEndPoint += ":5055";//we may use any port

            var authResponse = new AuthenticateResponse
            {
                MasterEndpoint = masterEndPoint,
                AuthenticationToken = NullStringToken,
                UserId = NullUserId,
                EncryptionData = encryptionData,
                Cluster = NullCluster,
                
            };

            var operation = (Operation)authRequest;
            var operationResponse = new OperationResponse(operation.OperationRequest.OperationCode, authResponse)
            {
                ReturnCode = 0
            };
            this.SendOperationResponse(operationResponse, sendParameters);
        }

        private void RedirectPeerToNullOnEncryptionOverload(EncryptionQueueFailureParams failureParams)
        {
            var encryptionData = NullPayloadEncryptionData;
            var masterEndPoint = this.LocalIPAddressIsIPv6 ? Settings.Default.IPv6NullAddress : Settings.Default.IPv4NullAddress;
            masterEndPoint += ":5055";//we may use any port

            var authResponse = new AuthenticateResponse
            {
                MasterEndpoint = masterEndPoint,
                AuthenticationToken = NullStringToken,
                UserId = NullUserId,
                EncryptionData = encryptionData,
                Cluster = NullCluster,
            };

            var operationResponse = new OperationResponse((byte)OperationCode.Authenticate, authResponse)
            {
                ReturnCode = 0,
                DebugMessage = failureParams.ErrorMsg
            };

            this.SendOperationResponse(operationResponse, failureParams.SendParameters);
        }

        protected override void HandleCustomAuthenticateResult(CustomAuthenticationResult customAuthResult, IAuthenticateRequest authenticateRequest, SendParameters sendParameters, AuthSettings authSettings)
        {
            var appAccount = (ApplicationAccount) authSettings;
            // try to get the master server instance for the specified application id
            CloudPhotonEndpointInfo masterServer;
            string message;
            if (
                !((PhotonCloudApp)this.application).CloudCache.TryGetPhotonEndpoint(
                    authenticateRequest,
                    appAccount,
                    out masterServer,
                    out message))
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat(
                        "MasterServer not found for region {0} on cloud {1} / service type {2}: {3}",
                        authenticateRequest.Region,
                        appAccount.PrivateCloud,
                        appAccount.ServiceType,
                        message);
                }

                var errorResponse = new OperationResponse(GetAuthOpCode(this.authOnceUsed))
                {
                    ReturnCode = (short)ErrorCode.InvalidRegion,
                    DebugMessage = string.Format("No connections allowed on region {0}.", authenticateRequest.Region)
                };

                this.SendOperationResponse(errorResponse, sendParameters);
                this.ScheduleDisconnect(this.GetDisconnectTime());
                return;
            }

            // TODO: V2: check if the master server is connected.
            // if (masterServer.IsOnline == false)
            // {
            // var opResponse = new OperationResponse { OperationCode = (byte)OperationCode.Authenticate, ReturnCode = (int)AuthenticationErrorCode.ApplicationOffline };
            // this.SendOperationResponse(opResponse, sendParameters);
            // return;
            // }

            var endpointProtocol = this.authOnceUsed
                ? (NetworkProtocolType)((Photon.NameServer.Operations.AuthOnceRequest)authenticateRequest).Protocol
                : this.NetworkProtocol;

            var masterEndPoint = masterServer.GetEndPoint(endpointProtocol, this.LocalPort,
                isIPv6: this.LocalIPAddressIsIPv6, useHostnames: this.IsIPv6ToIPv4Bridged);

            if (masterEndPoint == null)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Master server endpoint for protocol {0} not found for master server {1}.", endpointProtocol, masterServer);
                }

                var errorResponse = new OperationResponse(GetAuthOpCode(this.authOnceUsed))
                {
                    ReturnCode = (short)AuthErrorCode.ProtocolNotSupported,
                    DebugMessage = ErrorMessages.ProtocolNotSupported
                };

                this.SendOperationResponse(errorResponse, sendParameters);
                this.ScheduleDisconnect(this.GetDisconnectTime());
                return;
            }

            string userid;
            // the userid can be set 
            if (!string.IsNullOrEmpty(customAuthResult.UserId))
            {
                // by authentication service <<< overides client
                userid = customAuthResult.UserId;
            }
            else if (!string.IsNullOrEmpty(authenticateRequest.UserId))
            {
                // or through the client
                userid = authenticateRequest.UserId;
            }
            else
            {
                // we generate a userid
                userid = Guid.NewGuid().ToString();
            }

            // create auth token
            var unencryptedToken = this.application.TokenCreator.CreateAuthenticationToken(
                authenticateRequest, appAccount, userid, customAuthResult.AuthCookie);

            if (customAuthResult.ExpireAt.HasValue)
            {
                unencryptedToken.ExpireAtTicks = (UnixStart + TimeSpan.FromSeconds(customAuthResult.ExpireAt.Value)).Ticks;
            }
            else if (authenticateRequest.ClientAuthenticationType == (byte)ClientAuthenticationType.Xbox)
            {
                log.Debug(xboxCustomAuthLogGuard, "Custom auth response for XBox does not contain ExpireAt");
            }

            var authToken = this.GetEncryptedAuthToken(unencryptedToken, masterServer);

            this.CheckEncryptedToken(authToken, authenticateRequest, appAccount, masterServer);

            var authResponse = new AuthenticateResponse
            {
                MasterEndpoint = masterEndPoint,
                AuthenticationToken = authToken,
                Data = customAuthResult.Data,
                Nickname = customAuthResult.Nickname,
                UserId = userid,
                EncryptionData = this.authOnceUsed ? GetEncryptionData(unencryptedToken) : null,
                Cluster = masterServer.Cluster
            };

            var operationResponse = new OperationResponse(GetAuthOpCode(this.authOnceUsed), authResponse);
            this.SendOperationResponse(operationResponse, sendParameters);
        }

        private static readonly Version clientDotNetBinaryTokenVersion = new Version(5, 4, 1, 2);
        private static readonly Version clientNativeBinaryTokenVersion = new Version(5, 4, 0, 5);

        private object GetEncryptedAuthToken(AuthenticationToken unencryptedToken, CloudPhotonEndpointInfo masterServer)
        {
            var tc = this.application.TokenCreator;
            switch (this.SdkId)
            {
                case SdkIds.DotNet:
                    if (this.ClientVersion > clientDotNetBinaryTokenVersion)
                    {
                        return tc.EncryptAuthenticationTokenBinary(unencryptedToken, false);
                    }
                    break;
                case SdkIds.Native:
                    if (this.ClientVersion > clientNativeBinaryTokenVersion)
                    {
                        return tc.EncryptAuthenticationTokenBinary(unencryptedToken, false);
                    }
                    break;
            }

            return masterServer.UseV1Token ? tc.EncryptAuthenticationToken(unencryptedToken, false) :
                tc.EncryptAuthenticationTokenV2(unencryptedToken, false);
        }

        protected override void OnEncryptionQueueOverload(EncryptionQueueFailureParams failureParams)
        {
            this.RedirectPeerToNullOnEncryptionOverload(failureParams);
        }

        public new void ScheduleDisconnect(int delay = 3000)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Peer will be disconnected in {0}", delay);
            }

            base.ScheduleDisconnect(delay);
        }

        private VAppsCustomAuthHandler GetCustomAuthHandler(ApplicationAccount account)
        {
            var app = (PhotonCloudApp)this.application;
            var virtualApp = app.VirtualAppCache.GetOrCreateVirtualApp(account.ApplicationId);
            return app.CustomAuthenticationCache.GetOrCreateHandler(account.ApplicationId, virtualApp);
        }

        #endregion
    }
}