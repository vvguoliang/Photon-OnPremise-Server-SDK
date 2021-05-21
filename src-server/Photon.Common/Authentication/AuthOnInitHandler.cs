using System;
using ExitGames.Logging;
using Newtonsoft.Json;
using Photon.SocketServer;
using Photon.SocketServer.Diagnostics;
using Photon.SocketServer.Security;

namespace Photon.Common.Authentication
{
    public static class AuthOnInitHandler
    {
        #region fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly LogCountGuard logExceptionCountGuard = new LogCountGuard(new TimeSpan(0, 1, 0));
        private static readonly LogCountGuard logSetupCountGuard = new LogCountGuard(new TimeSpan(0, 1, 0));
        private static readonly LogCountGuard canNotDecryptLogGuard = new LogCountGuard(new TimeSpan(0, 1, 0));

        private static readonly byte[] EmptyByteArray = new byte[0];
        #endregion

        public static AuthenticationToken DoAuthUsingInitObject(string token, ClientPeer peer, InitRequest initRequest, AuthTokenFactory tokenFactory, out ErrorCode errorCode, out string errorMsg)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Peer performs auth using init object. p:{0}", peer);
            }

            var authToken = GetValidAuthToken(token, peer, tokenFactory, out errorCode, out errorMsg);

            if (authToken == null)
            {
                return null;
            }

            errorCode = SetupEncryption(authToken, out errorMsg, peer, initRequest);

            if (errorCode != ErrorCode.Ok)
            {
                return null;
            }

            return authToken;

        }

        #region Methods

        private static ErrorCode SetupEncryption(AuthenticationToken token, out string errorMsg, ClientPeer peer, InitRequest initRequest)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("setting up encryption. p:{0}", peer);
            }

            var encryptionDataDict = token.EncryptionData;
            errorMsg = string.Empty;
            
            if (encryptionDataDict == null)
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat(logSetupCountGuard, "AuthOnInitHandler: expected encryption data not provided. appId:{0}/{1}, p:{2}", 
                        token.ApplicationId, token.ApplicationVersion, peer);
                }

                errorMsg = string.Format(ErrorMessages.InvalidEncryptionData, "expected encryption data not provided");
                return ErrorCode.InvalidEncryptionParameters;
            }

            var encryptionData = new EncryptionData(peer.Protocol, encryptionDataDict);
            if (!encryptionData.IsValid)
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat(logSetupCountGuard,
                        "AuthOnInitHandler: Invalid encryption data. ErrorMsg:{4}. appId:{0}/{1}, data:{2}, p:{3}",
                        token.ApplicationId, token.ApplicationVersion, JsonConvert.SerializeObject(encryptionDataDict), peer, encryptionData.GetErrorMessage());
                }
                errorMsg = string.Format(ErrorMessages.InvalidEncryptionData, encryptionData.GetErrorMessage());
                return ErrorCode.InvalidEncryptionParameters;
            }

            var mode = (EncryptionModes)encryptionData.EncryptionMode;

            try
            {
                switch (mode)
                {
                    case EncryptionModes.PayloadEncryption:
                    case EncryptionModes.PayloadEncryptionWithIV:
                    case EncryptionModes.PayloadEncryptionWithIVHMAC:
                        SetupUserDataEncryptionWithoutDH(encryptionData, peer);
                        break;
                    case EncryptionModes.DatagramEncyption:
                    case EncryptionModes.DatagramEncyptionWithRandomInitialNumbers:
                    case EncryptionModes.DatagramEncyptionGCMWithRandomInitialNumbers:
                        if (peer.NetworkProtocol != NetworkProtocolType.Udp)
                        {
                            errorMsg = ErrorMessages.EncryptionModeMismatch;
                            return ErrorCode.InvalidEncryptionParameters;
                        }

                        SetupUdpEncryption(encryptionData, peer, initRequest);
                        break;
                    default:
                    {
                        if (log.IsWarnEnabled)
                        {
                            log.WarnFormat(logSetupCountGuard,
                                $"AuthOnInitHandler: Unknown encryption mode: '{mode}'. appId:{0}/{1}, data:{2}, p:{3}",
                                token.ApplicationId, token.ApplicationVersion, JsonConvert.SerializeObject(encryptionDataDict), peer);
                        }
                        errorMsg = string.Format(ErrorMessages.InvalidEncryptionData, $"Unknown Encryption mode {mode}");
                        return ErrorCode.InvalidEncryptionParameters;
                    }
                }
            }
            catch (Exception e)
            {
                errorMsg = e.ToString();
                var msg = string.Format("AuthOnInitHandler: Exception during encryption setup. appId:{0}/{1}, Data: {2}, p:{3}", 
                    token.ApplicationId, token.ApplicationVersion, JsonConvert.SerializeObject(encryptionDataDict), peer);

                log.Error(logExceptionCountGuard, msg, e);
                return ErrorCode.InvalidEncryptionParameters;
            }
            return ErrorCode.Ok;
        }

        private static void SetupUdpEncryption(EncryptionData encryptionData, ClientPeer peer, InitRequest initRequest)
        {
            var secret1 = encryptionData.EncryptionSecret;
            var secret2 = encryptionData.AuthSecret ?? EmptyByteArray;

            var uintInitialSeqNums = EncryptionData.GetInitialSeqNums(encryptionData, initRequest.ChannelCount);
            peer.SetEncryption(secret1, secret2, uintInitialSeqNums);
        }

        private static void SetupUserDataEncryptionWithoutDH(EncryptionData encryptionData, ClientPeer peer)
        {
#pragma warning disable 618
            peer.SetupPayloadEncryption(encryptionData.EncryptionSecret);
#pragma warning restore 618
        }

        private static AuthenticationToken GetValidAuthToken(string tokenString,
            ClientPeer peer, AuthTokenFactory tokenFactory, out ErrorCode errorCode, out string errorMsg)
        {
            errorCode = ErrorCode.Ok;
            errorMsg = string.Empty;

            if (tokenFactory == null)
            {
                log.ErrorFormat(logSetupCountGuard, "AuthOnInitHandler: Token factory is NOT setup.AuthTokenKey not specified in config. p:{0}", peer);

                errorCode = ErrorCode.InvalidAuthentication;
                errorMsg = ErrorMessages.AuthTokenTypeNotSupported;

                return null;
            }

            // validate the authentication token
            if (string.IsNullOrEmpty(tokenString))
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("failed to get token. tokenString is empty. p:{0}", peer);
                }

                errorCode = ErrorCode.InvalidAuthentication;
                errorMsg = ErrorMessages.AuthTokenMissing;

                return null;
            }

            AuthenticationToken authToken;
            if (!tokenFactory.DecryptAuthenticationToken(tokenString, out authToken, out errorMsg))
            {
                log.WarnFormat(canNotDecryptLogGuard, "AuthOnInitHandler: Could not decrypt authenticaton token. ErrorMsg:{0}, Token: {1}, p:{2}",
                    errorMsg, tokenString, peer);

                errorCode = ErrorCode.InvalidAuthentication;
                errorMsg = ErrorMessages.AuthTokenTypeNotSupported;

                return null;
            }

            if (authToken.ExpireAtTicks < DateTime.UtcNow.Ticks)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("failed to get token. token is expired. p:{0}", peer);
                }

                errorCode = ErrorCode.InvalidAuthentication;
                errorMsg = ErrorMessages.AuthTokenExpired;

                return null;
            }

            return authToken;
        }

        #endregion

    }
}