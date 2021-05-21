// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ClientPeer.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using Photon.Common.Authentication;
using Photon.Common.Authentication.CustomAuthentication;
using Photon.Common;
using Photon.SocketServer.Diagnostics;

namespace Photon.NameServer
{
    using System;
    using System.Collections.Generic;

    using ExitGames.Logging;
    using Photon.SocketServer;
    using Photon.SocketServer.Rpc;

    using PhotonHostRuntimeInterfaces;

    using AuthenticateRequest = Photon.NameServer.Operations.AuthenticateRequest;
    using AuthenticateResponse = Photon.NameServer.Operations.AuthenticateResponse;
    using Operations;

    public class ClientPeer : Photon.SocketServer.ClientPeer, ICustomAuthPeer
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        protected static readonly LogCountGuard customAuthIsNotSetupLogGuard = new LogCountGuard(new TimeSpan(0, 0, 1));

        protected readonly PhotonApp application;

        // store the client's App ID for debugging purposes (only used for log messages). 
        protected string authenticatedApplicationId;

        protected bool authOnceUsed;

        private static readonly LogCountGuard exceptionGuard = new LogCountGuard(new TimeSpan(0, 0, 30));
        private static readonly LogCountGuard authOnceGuard = new LogCountGuard(new TimeSpan(0, 0, 30));

        protected readonly int MinDisconnectTime = Photon.Common.Settings.Default.MinDisconnectTime;
        protected readonly int MaxDisconnectTime = Photon.Common.Settings.Default.MaxDisconnectTime;

        private readonly Random random = new Random();

        #endregion

        #region .ctor

        public ClientPeer(PhotonApp application, InitRequest initRequest)
            : base(initRequest)
        {
            this.application = application;

            var initObjAsDict = initRequest.InitObject as Dictionary<byte, object>;
            if (initObjAsDict != null)
            {
                this.RequestFiber.Enqueue(() =>
                {
                    var authRequest = new AuthOnceRequest(this.Protocol, new OperationRequest((byte)OperationCode.AuthOnce, initObjAsDict));

                    this.HandleAuthenticateRequest(authRequest, new SendParameters { Encrypted = true }, (NetworkProtocolType)authRequest.Protocol);
                });
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("auth from init request initiated. p:{0}", this);
                }
            }
            else
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("no auth from init request. wait for old way auth p:{0}", this);
                }
            }
        }

        #endregion

        #region Properties

        public string UserId { get; set; }

        #endregion

        #region Implemented Interfaces

        #region IAuthenticationPeer

        public virtual void OnCustomAuthenticationError(ErrorCode errorCode, string debugMessage, IAuthenticateRequest authenticateRequest, SendParameters sendParameters)
        {
            this.RequestFiber.Enqueue(() => this.DoCustomAuthenticationError(errorCode, debugMessage, authenticateRequest, sendParameters));
        }

        public virtual void OnCustomAuthenticationResult(CustomAuthenticationResult customAuthResult, IAuthenticateRequest authenticateRequest, SendParameters sendParameters, object state)
        {
            var authSettings = (AuthSettings)state;
            this.RequestFiber.Enqueue(() => this.DoCustomAuthenticationResult(customAuthResult, authenticateRequest, 
                sendParameters, authSettings));
        }

        #endregion

        #endregion

        #region Methods

        protected override void OnDisconnect(DisconnectReason reasonCode, string reasonDetail)
        {
            //TODO Check why we don't dispose peer on disconnect?
            if (log.IsDebugEnabled && reasonCode != DisconnectReason.ClientDisconnect)
            {
                log.DebugFormat("OnDisconnect with {0}:'{5}' appId={1},protocol={2},remoteEndpoint={3}:{4}", 
                    reasonCode, this.authenticatedApplicationId, this.NetworkProtocol, this.RemoteIP, this.RemotePort, reasonDetail);
            }
        }

        protected override void OnOperationRequest(OperationRequest operationRequest, SendParameters sendParameters)
        {
            try
            {
                Dictionary<byte, object> dict = operationRequest.Parameters;
                foreach (object value in dict.Values)
                {
                    log.Info("============ClientPeer==========:" + value.ToString());
                }

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

                    default:
                        {
                            log.WarnFormat("Unknown operation {0}. Client info: appId={1},protocol={2},remoteEndpoint={3}:{4}", operationCode, this.authenticatedApplicationId, this.NetworkProtocol, this.RemoteIP, this.RemotePort);
                            this.HandleUnknownOperationCode(operationRequest, sendParameters);
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

        protected OperationResponse HandleAuthOnceRequest(OperationRequest operationRequest, SendParameters sendParameters)
        {
            this.authOnceUsed = true;
            var authOnceRequest = new AuthOnceRequest(this.Protocol, operationRequest);
            if (!sendParameters.Encrypted && !this.IsConnectionSecure && this.NetworkProtocol != NetworkProtocolType.WebRTC)
            {
                log.WarnFormat(authOnceGuard, "Got AuthOnce Request from client over unsecure channel. appId:{0}, clientVer:{1}, SdkId:{2}, Debug:{3}, ProtocolType:{4}",
                    authOnceRequest.ApplicationId, this.ClientVersion, this.SdkId, this.ClientUsingDebugLib, this.ProtocolType);

                return new OperationResponse(operationRequest.OperationCode)
                {
                    DebugMessage = "AuthOnce can not be used over unsecure channel",
                    ReturnCode = (short)ErrorCode.InvalidAuthentication,
                };
            }


            return this.HandleAuthenticateRequest(authOnceRequest, sendParameters, (NetworkProtocolType)authOnceRequest.Protocol);
        }

        protected OperationResponse HandleAuthenticateRequest(OperationRequest operationRequest, SendParameters sendParameters)
        {
            // validate operation request
            var authenticateRequest = new AuthenticateRequest(this.Protocol, operationRequest);

            return this.HandleAuthenticateRequest(authenticateRequest, sendParameters, this.NetworkProtocol);
        }

        protected virtual OperationResponse HandleAuthenticateRequest(AuthenticateRequest authenticateRequest,
            SendParameters sendParameters, NetworkProtocolType endpointProtocol)
        {
            if (authenticateRequest.IsValid == false)
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


            /////
            if (this.Connected == false)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("OnGetApplicationAccount: Client disconnected. Ignore response.");
                }
                return null;
            }

            var operationRequest = authenticateRequest.OperationRequest;

            if (log.IsDebugEnabled)
            {
                log.DebugFormat(
                    "HandleAuthenticateRequest for App ID {0}",
                    authenticateRequest.ApplicationId);
            }

            // store for debugging purposes. 
            this.authenticatedApplicationId = authenticateRequest.ApplicationId;

            // try to get the master server instance for the specified application id
            PhotonEndpointInfo masterServer;
            string message;
            if (!this.application.ServerCache.TryGetPhotonEndpoint(authenticateRequest.Region, out masterServer, out message))
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("MasterServer not found for: {0}. AppId: {1}", message, authenticateRequest.ApplicationId);
                }
                //CHECK why only invalid region err?
                this.SendOperationResponse(new OperationResponse(operationRequest.OperationCode)
                {
                    ReturnCode = (short)ErrorCode.InvalidRegion,
                    //DebugMessage =
                    //    string.Format("Cloud {0} / Region {1} is not available.", applicationAccount.PrivateCloud, authenticateRequest.Region)
                }, sendParameters);
                return null;
            }

            //TODO change
            string masterEndPoint;
            try
            {
                masterEndPoint = masterServer.GetEndPoint(endpointProtocol, this.LocalPort,
               isIPv6: this.LocalIPAddressIsIPv6, useHostnames: this.IsIPv6ToIPv4Bridged);
            }
            catch (Exception e)
            {
                //webrtc
                masterEndPoint = masterServer.GetEndPoint(endpointProtocol, 0);

                var str = string.Format(
                    "Handle Auth: Exception during GetEndPoint call. EndPoint protocol:{0}, LocalPort:{1}, isIpV6:{2}, useHostNames:{3}",
                    endpointProtocol, this.LocalPort, this.LocalIPAddressIsIPv6, this.IsIPv6ToIPv4Bridged);

                log.Warn(exceptionGuard, str, e);
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Endpoint found 1 - Hostname {0}, UDP: {1}, HTTP: {2}", masterServer.UdpHostname, masterServer.UdpEndPoint, masterServer.HttpEndPoint);
            }

            if (masterEndPoint == null)
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("Master server endpoint for protocol {0} not found on master server {1}.", this.NetworkProtocol, masterServer);
                }

                this.SendOperationResponse(new OperationResponse(operationRequest.OperationCode)
                {
                    ReturnCode = (short)AuthErrorCode.ProtocolNotSupported,
                    DebugMessage = ErrorMessages.ProtocolNotSupported
                }, sendParameters);
                return null;
            }

            ///
            // check if custom client authentication is required
            if (this.application.CustomAuthHandler.IsClientAuthenticationEnabled)
            {
                if (this.application.TokenCreator == null)
                {
                    log.WarnFormat("No custom authentication supported: AuthTokenKey not specified in config.");

                    var resp = new OperationResponse(authenticateRequest.OperationRequest.OperationCode)
                    {
                        ReturnCode = (short)ErrorCode.InvalidAuthentication,
                        DebugMessage = ErrorMessages.AuthTokenTypeNotSupported
                    };
                    this.SendOperationResponse(resp, sendParameters);
                    return null;
                }

                var authSettings = new AuthSettings
                {
                    IsAnonymousAccessAllowed = this.application.CustomAuthHandler.IsAnonymousAccessAllowed,
                };

                this.application.CustomAuthHandler.AuthenticateClient(this, authenticateRequest, authSettings, sendParameters, authSettings);
                return null;
            }

            var response = this.HandleDefaultAuthenticateRequest(authenticateRequest, masterEndPoint,new AuthSettings()); //TODO Check
            this.SendOperationResponse(response, sendParameters);

            //authenticate application id
            return null;
        }

        private OperationResponse HandleDefaultAuthenticateRequest(AuthenticateRequest authenticateRequest, string masterEndPoint, AuthSettings authSettings)
        {
            // generate a userid if its not set by the client
            var userId = string.IsNullOrEmpty(authenticateRequest.UserId) ? Guid.NewGuid().ToString() : authenticateRequest.UserId;
            // create auth token
            var unencryptedToken = this.application.TokenCreator.CreateAuthenticationToken(authenticateRequest, authSettings,
                userId, new Dictionary<string, object>());

            var authToken = this.GetEncryptedAuthToken(unencryptedToken);

            var authResponse = new AuthenticateResponse
            {
                MasterEndpoint = masterEndPoint,
                AuthenticationToken = authToken,
                UserId = userId,
                EncryptionData = GetEncryptionData(unencryptedToken),
            };

            return new OperationResponse(authenticateRequest.OperationRequest.OperationCode, authResponse);
        }

        private OperationResponse HandleGetRegionListRequest(OperationRequest operationRequest, SendParameters sendParameters)
        {
            var regionListRequest = new GetRegionListRequest(this.Protocol, operationRequest);
            if (regionListRequest.IsValid == false)
            {
                this.HandleInvalidOperation(regionListRequest, sendParameters);
                return null;
            }

            List<string> regions;
            List<string> endPoints;
            string message;

            // authenticate application id
            if (!this.application.ServerCache.TryGetRegions(regionListRequest, this.NetworkProtocol, this.LocalPort, this.LocalIPAddressIsIPv6, 
                this.IsIPv6ToIPv4Bridged, out regions, out endPoints, out message))
            {
                this.SendOperationResponse(new OperationResponse((byte) OperationCode.GetRegionList)
                {
                    ReturnCode = (short) ErrorCode.InvalidRegion,
                    DebugMessage = message
                }, sendParameters);

                this.ScheduleDisconnect(this.GetDisconnectTime());
                return null;
            }
            
            var regionListResponse = new GetRegionListResponse
            {
                Endpoints = endPoints.ToArray(),
                Region = regions.ToArray()
            };


            this.SendOperationResponse(new OperationResponse((byte) OperationCode.GetRegionList, regionListResponse), sendParameters);

            return null;
        }

        protected void HandleInvalidOperation(Operation operation, SendParameters sendParameters)
        {
            string errorMessage = operation.GetErrorMessage();

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Invalid operation: OpCode={0}; {1}", operation.OperationRequest.OperationCode, errorMessage);
            }

            var response = new OperationResponse
                {
                   OperationCode = operation.OperationRequest.OperationCode, ReturnCode = (short)ErrorCode.OperationInvalid, DebugMessage = errorMessage 
                };

            this.SendOperationResponse(response, sendParameters);
        }

        protected void HandleUnknownOperationCode(OperationRequest operationRequest, SendParameters sendParameters)
        {
            var operationResponse = new OperationResponse
                {
                   OperationCode = operationRequest.OperationCode, ReturnCode = (short)ErrorCode.OperationInvalid, DebugMessage = "Unknown operation code" 
                };

            this.SendOperationResponse(operationResponse, sendParameters);
        }

        protected virtual void DoCustomAuthenticationError(ErrorCode errorCode, string debugMessage, IAuthenticateRequest authenticateRequest, SendParameters sendParameters)
        {
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

                var errorResponse = new OperationResponse(GetAuthOpCode(this.authOnceUsed)) { ReturnCode = (short)errorCode, DebugMessage = debugMessage };
                this.SendOperationResponse(errorResponse, sendParameters);
            }
            catch (Exception ex)
            {
                log.Error(ex);

                var errorResponse = new OperationResponse(GetAuthOpCode(this.authOnceUsed)) { ReturnCode = (short)ErrorCode.InternalServerError };
                this.SendOperationResponse(errorResponse, sendParameters);
            }

            this.ScheduleDisconnect(this.GetDisconnectTime());
        }

        protected virtual void DoCustomAuthenticationResult(CustomAuthenticationResult customAuthResult, IAuthenticateRequest authenticateRequest, SendParameters sendParameters, AuthSettings authSettings)
        {
            if (customAuthResult == null)
            {
                log.WarnFormat("Custom authentication error. customAuthResult is null. appId={0}/{1}",
                    authenticateRequest.ApplicationId, authenticateRequest.ApplicationVersion);
                return;
            }

            try
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat(
                    "DoCustomAuthenticationResult: appId={0}, resultCode={1}, message={2}, client connected:{3}",
                        authenticateRequest.ApplicationId,
                        customAuthResult.ResultCode,
                    customAuthResult.Message,
                    this.Connected);
                }

                if (!this.Connected)
                {
                    return;
                }

                OperationResponse operationResponse;

                switch (customAuthResult.ResultCode)
                {
                    default:
                        operationResponse = new OperationResponse(GetAuthOpCode(this.authOnceUsed))
                        {
                            ReturnCode = (short)ErrorCode.CustomAuthenticationFailed,
                            DebugMessage = customAuthResult.Message
                        };
                        this.SendOperationResponse(operationResponse, new SendParameters());
                        return;

                    case CustomAuthenticationResultCode.Data:
                        var auth = new AuthenticateResponse { Data = customAuthResult.Data };
                        operationResponse = new OperationResponse(GetAuthOpCode(this.authOnceUsed), auth);
                        this.SendOperationResponse(operationResponse, sendParameters);
                        return;

                    case CustomAuthenticationResultCode.Ok:
                        this.HandleCustomAuthenticateResult(customAuthResult, authenticateRequest, sendParameters, authSettings);
                        return;
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                var errorResponse = new OperationResponse(GetAuthOpCode(this.authOnceUsed))
                {
                    ReturnCode = (short)ErrorCode.InternalServerError,
                    DebugMessage = ex.Message,
                };
                this.SendOperationResponse(errorResponse, sendParameters);
            }
        }

        protected virtual void HandleCustomAuthenticateResult(CustomAuthenticationResult customAuthResult, IAuthenticateRequest authenticateRequest, SendParameters sendParameters, AuthSettings authSettings)
        {
            //try to get the master server instance for the specified application id


           PhotonEndpointInfo masterServer;
            string message;
            if (!this.application.ServerCache.TryGetPhotonEndpoint(authenticateRequest.Region, out masterServer,  out message))
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat(
                        "MasterServer not found for service type {2}: {3}",
                        authenticateRequest.Region,
                        message);
                }

                var errorResponse = new OperationResponse(GetAuthOpCode(this.authOnceUsed))
                {
                    ReturnCode = (short)ErrorCode.InvalidRegion,
                    DebugMessage = string.Format("No connections allowed on region {0}.", authenticateRequest.Region)
                };

                this.SendOperationResponse(errorResponse, sendParameters);
                return;
            }

            var nameServerAuthRequest = (AuthenticateRequest)authenticateRequest;
            var isAuthOnceUsed = nameServerAuthRequest.OperationRequest.OperationCode != (byte)OperationCode.Authenticate;
            var endpointProtocol = isAuthOnceUsed ? (NetworkProtocolType)((AuthOnceRequest)nameServerAuthRequest).Protocol : this.NetworkProtocol;
            string masterEndPoint;
            //tmp webrtc
            try
            {
                masterEndPoint = masterServer.GetEndPoint(endpointProtocol, this.LocalPort,
                isIPv6: this.LocalIPAddressIsIPv6, useHostnames: this.IsIPv6ToIPv4Bridged);
            }
            catch (Exception e)
            {
                masterEndPoint = masterServer.GetEndPoint(endpointProtocol, 0);

                var str = string.Format(
                    "Custom Auth: Exception during GetEndPoint call. EndPoint protocol:{0}, LocalPort:{1}, isIpV6:{2}, useHostNames:{3}",
                    endpointProtocol, this.LocalPort, this.LocalIPAddressIsIPv6, this.IsIPv6ToIPv4Bridged);

                log.Warn(exceptionGuard, str, e);
            }

            if (masterEndPoint == null)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Master server endpoint for protocol {0} not found for master server {1}.", this.NetworkProtocol, masterServer);
                }

                var errorResponse = new OperationResponse(GetAuthOpCode(this.authOnceUsed))
                {
                    ReturnCode = (short)AuthErrorCode.ProtocolNotSupported,
                    DebugMessage = ErrorMessages.ProtocolNotSupported
                };

                this.SendOperationResponse(errorResponse, sendParameters);
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
                authenticateRequest, new AuthSettings() , userid, customAuthResult.AuthCookie);

            var authToken = this.GetEncryptedAuthToken(unencryptedToken);

            var authResponse = new AuthenticateResponse
            {
                MasterEndpoint = masterEndPoint,
                AuthenticationToken = authToken,
                Data = customAuthResult.Data,
                Nickname = customAuthResult.Nickname,
                UserId = userid,
                EncryptionData = GetEncryptionData(unencryptedToken),
            };

            var operationResponse = new OperationResponse(GetAuthOpCode(this.authOnceUsed), authResponse);
            this.SendOperationResponse(operationResponse, sendParameters);
        }

        private object GetEncryptedAuthToken(AuthenticationToken unencryptedToken)
        {
            return this.application.TokenCreator.EncryptAuthenticationTokenV2(unencryptedToken, false);
        }


        protected static Dictionary<byte, object> GetEncryptionData(AuthenticationToken unencryptedToken)
        {
            return unencryptedToken.EncryptionData;
        }

        protected void SendInternalErrorResponse(OperationRequest operationRequest, SendParameters sendParameters, string msg)
        {
            var operationResponse = new OperationResponse
                {
                    OperationCode = operationRequest.OperationCode,
                    ReturnCode = (short)ErrorCode.InternalServerError,
                    DebugMessage = msg, 
                };

            this.SendOperationResponse(operationResponse, sendParameters);
        }

        protected static byte GetAuthOpCode(bool authOnce)
        {
            return (byte)(authOnce ? OperationCode.AuthOnce : OperationCode.Authenticate);
        }


        protected int GetDisconnectTime()
        {
            return this.GetDisconnectTime(this.MinDisconnectTime, this.MaxDisconnectTime);
        }

        protected int GetDisconnectTime(int minDisconnectTime, int maxDisconnectTime)
        {
            return this.random.Next(minDisconnectTime, maxDisconnectTime);
        }
        #endregion
    }
}