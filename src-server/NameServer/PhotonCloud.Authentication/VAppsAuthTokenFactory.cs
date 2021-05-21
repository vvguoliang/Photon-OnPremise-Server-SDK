using System;
using Photon.Common.Authentication;
using Photon.Common.Authentication.Encryption;
using Photon.SocketServer.Diagnostics;

namespace PhotonCloud.Authentication
{
    using System.Collections.Generic;

    using ExitGames.Logging;

    public class VAppsAuthTokenFactory : AuthTokenFactory
    {
// ReSharper disable once UnusedMember.Local
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public override AuthenticationToken CreateAuthenticationToken(IAuthenticateRequest authRequest, AuthSettings authSettings, string userId, Dictionary<string, object> authCookie)
        {
            var authResult = (ApplicationAccount)authSettings;

            var token = this.CreateAuthenticationToken(authResult, authRequest, userId, authCookie);

            token.MaxCcu = authResult.MaxCcu;
            token.IsCcuBurstAllowed = authResult.IsCcuBurstAllowed;
            token.PrivateCloud = authResult.PrivateCloud;

            var authOnceRequest = authRequest as IAuthOnceRequest;
            if (authOnceRequest != null)
            {
                token.EncryptionData = EncryptionDataGenerator.Generate(authOnceRequest.EncryptionMode);
            }
            
            return token;
        }

        public static void CheckEncryptedToken(AuthTokenFactory tokenFactory, LogCountGuard appCheckGuard, object authToken, IAuthenticateRequest authenticateRequest, 
            ApplicationAccount applicationAccount, bool useV1Token)
        {
            var strToken = authToken as string;

            if (string.IsNullOrEmpty(strToken))
            {
                return;
            }

            AuthenticationToken token;
            string errorMsg;
            if (useV1Token)
            {
                if (!tokenFactory.DecryptAuthenticationTokenV1(strToken, out token, out errorMsg))
                {
                    log.WarnFormat(appCheckGuard, "AppId Check: Failed to decrypt just created V1 token. errorMsg:{3}, AppId:{0}/{1}, token:{2}",
                        authenticateRequest.ApplicationId, authenticateRequest.ApplicationVersion, authToken, errorMsg);
                    return;
                }
            }
            else
            {
                if (!tokenFactory.DecryptAuthenticationTokenV2(strToken, out token, out errorMsg))
                {
                    log.WarnFormat(appCheckGuard,
                        "AppId Check: Failed to decrypt just created V2 token. ErrorMsg:{3}, AppId:{0}/{1}, token:{2}",
                        authenticateRequest.ApplicationId, authenticateRequest.ApplicationVersion, authToken, errorMsg);
                    return;
                }
            }

            Guid guid;
            if (!Guid.TryParse(token.ApplicationId, out guid))
            {
                log.WarnFormat(appCheckGuard,
                    "AppId Check: Wrong appId in token after encryption. appId:{0}, account appId:{1}, request appId:{2}, token:{0}",
                    token.ApplicationId, applicationAccount.ApplicationId, authenticateRequest.ApplicationId, authToken);
            }
        }

        public static void CheckEncryptedToken(AuthTokenFactory tokenFactory, LogCountGuard appCheckGuard, object encryptedToken, 
            AuthenticationToken unencryptedToken, ApplicationAccount applicationAccount, bool binaryToken)
        {
            AuthenticationToken token;
            string errorMsg;

            if (binaryToken)
            {
                var binArray = encryptedToken as byte[];
                if (binArray == null)
                {
                    log.WarnFormat(appCheckGuard, "AppId Check: Failed to cast just created binary token to byte[]. AppId:{0}/{1}, token:'{2}'",
                        unencryptedToken.ApplicationId, unencryptedToken.ApplicationVersion, encryptedToken);
                    return;
                }
                if (!tokenFactory.DecryptAuthenticationTokenBynary(binArray, 0, binArray.Length, out token, out errorMsg))
                {
                    log.WarnFormat(appCheckGuard, "AppId Check: Failed to decrypt just created binary token. errorMsg:{0}, AppId:{1}/{2}, " +
                        "unencryptedToken:{3}, token:{4}", errorMsg, unencryptedToken.ApplicationId, unencryptedToken.ApplicationVersion, 
                        Newtonsoft.Json.JsonConvert.SerializeObject(unencryptedToken), BitConverter.ToString(binArray));
                    return;
                }
            }
            else
            {
                var strToken = encryptedToken as string;

                if (string.IsNullOrEmpty(strToken))
                {
                    return;
                }

                if (!tokenFactory.DecryptAuthenticationToken(strToken, out token, out errorMsg))
                {
                    log.WarnFormat(appCheckGuard, "AppId Check: Failed to decrypt just created token. errorMsg:{0}, AppId:{1}/{2}, " +
                        "unencryptedToken:{3}, token:{4}", errorMsg, unencryptedToken.ApplicationId, unencryptedToken.ApplicationVersion, 
                        Newtonsoft.Json.JsonConvert.SerializeObject(unencryptedToken), strToken);
                    return;
                }
            }

            Guid guid;
            if (!Guid.TryParse(token.ApplicationId, out guid))
            {
                log.WarnFormat(appCheckGuard,
                    "AppId Check: Wrong appId in token after encryption. appId:{0}, account appId:{1}, unencryptedToken:{2}, token:{0}",
                    token.ApplicationId, applicationAccount.ApplicationId, Newtonsoft.Json.JsonConvert.SerializeObject(unencryptedToken), encryptedToken);
            }
        }

        /// <summary>
        /// Create a renewed Authentication Token on Master server - to be validated on GS
        /// </summary>
        /// <returns></returns>
        private VAppsAuthenticationToken CreateAuthenticationToken(ApplicationAccount account, IAuthenticateRequest authRequest,
            string userId, Dictionary<string, object> authCookie = null)
        {
            var token = new VAppsAuthenticationToken
                            {
                                ApplicationId = account.ApplicationId,
                                ApplicationVersion = authRequest.ApplicationVersion,
                                MaxCcu = int.MaxValue,
                                IsCcuBurstAllowed = true,
                                UserId = userId,
                                PrivateCloud = null,
                                AuthCookie = new Dictionary<string, object>(),
                                HasExternalApi = account.HasExternalApi,
                                Flags = authRequest.Flags,
                                CustomAuthProvider = authRequest.ClientAuthenticationType,
                            };

            this.SetupToken(token);
            if (authCookie != null)
            {
                token.AuthCookie = authCookie;
            }
            return token;
        }
       
        protected override bool TryDeserializeToken(byte[] tokenData, out AuthenticationToken authToken, out string errorMsg)
        {
            return VAppsAuthenticationToken.TryDeserialize(tokenData, out authToken, out errorMsg);
        }
    }
}
