// --------------------------------------------------------------------------------------------------------------------
// <copyright file="VirtualAppAuthenticationHandler.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the VirtualAppAuthenticationHandler type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Web;
using ExitGames.Concurrency.Fibers;
using ExitGames.Threading;
using Jose;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Photon.Cloud.Common.Diagnostic;
using Photon.Common;
using Photon.Common.Authentication;
using Photon.Common.Authentication.CustomAuthentication;
using Photon.Common.Authentication.Data;
using Photon.Common.Authentication.Diagnostic;
using Photon.SocketServer.Diagnostics;
using Photon.SocketServer.Security;
using PhotonCloud.Authentication.CustomAuth;
using PhotonCloud.Authentication.CustomAuth.Diagnostic;

namespace PhotonCloud.Authentication
{
    using System;
    using System.Collections.Generic;
    using ExitGames.Logging;
    using Photon.SocketServer;
    public class VAppsCustomAuthHandler : CustomAuthHandler
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private readonly LogCountGuard customAuthNotConfigured = new LogCountGuard(new TimeSpan(1, 0, 0));

        private ApplicationAccount lastApplicationAccount;

        private readonly IVACustomAuthCounters counters;

        private Decryptor PlayerIoDecryptor = null;
        private Decryptor PlayerIoDecryptor2 = null;

        private HMACSHA256 PlayerIoSignatureHmac;
        private HMACSHA256 PlayerIoSignatureHmac2;

        //for JWT authentication
        public Decryptor Decryptor = null;
        public Decryptor Decryptor2 = null;

        public HMACSHA256 SignatureHmac = null;
        public HMACSHA256 SignatureHmac2 = null;

        public VAppsCustomAuthHandler(string applicationId, string applicationVersion, IHttpRequestQueueCountersFactory factory,
            IVACustomAuthCounters counters = null) :
            base(factory ?? new HttpRequestQueueCountersFactory(),
                new PoolFiber(new BeforeAfterExecutor(
                () =>
                {
                    LogTagsSetup.AddAppIdTags(applicationId, applicationVersion);
                },
                () => log4net.ThreadContext.Properties.Clear()))
            )
        {
            this.counters = counters;
        }

        private static string MakeInstanceName(string enumValue)
        {
            return ApplicationBase.Instance.PhotonInstanceName + "_" + enumValue;
        }

        protected override void OnAuthenticateClient(ICustomAuthPeer peer, IAuthenticateRequest authRequest, AuthSettings authSettings, SendParameters sendParameters, object state)
        {
            try
            {
                var applicationAccount = (ApplicationAccount)authSettings;
                if (this.lastApplicationAccount == null || this.lastApplicationAccount != applicationAccount)
                {
                    this.InitializeAuthServices(applicationAccount);
                    this.lastApplicationAccount = applicationAccount;
                }

                // ReSharper disable once PossibleInvalidOperationException
                var authenticationType = (ClientAuthenticationType)authRequest.ClientAuthenticationType;

                //temporary logging for possible strict check need
                if (authenticationType != ClientAuthenticationType.Custom && !this.authenticationServices.ContainsKey(authenticationType))
                {
                    //log this only for the consoles
                    switch (authenticationType)
                    {
                        case ClientAuthenticationType.PlayStation:
                        case ClientAuthenticationType.Xbox:
                        case ClientAuthenticationType.Steam:
                        case ClientAuthenticationType.Oculus:
                        case ClientAuthenticationType.Viveport:
                        case ClientAuthenticationType.Nintendo:
                            if (log.IsWarnEnabled)
                            {
                                log.WarnFormat(customAuthNotConfigured,
                                    "Authenticate client: requested not configured authenticationType '{0}', ApplicationId {1}",
                                    authenticationType, applicationAccount.ApplicationId);
                            }
                            break;
                    }
                }

                //add a switch statement?
                if (authenticationType == ClientAuthenticationType.Nintendo)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("OnAuthenticateClient - Nintendo");
                    }

                    IClientAuthenticationQueue authQueue;
                    if (!this.authenticationServices.TryGetValue(authenticationType, out authQueue))
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Authentication type not supported: {0} for AppId={1}/{2}", authenticationType, authRequest.ApplicationId, authRequest.ApplicationVersion);
                        }

                        this.OnCustomAuthenticationError(peer, authRequest, sendParameters, null, "Authentication type not supported");
                        return;
                    }

                    var queueState = new AuthQueueState(peer, authRequest, sendParameters, state);
                    this.NintendoAuthenticateClient(authQueue, authRequest, queueState);
                    return;
                }

                if (authenticationType == ClientAuthenticationType.Jwt)
                {
                    IClientAuthenticationQueue authQueue;
                    if (!this.authenticationServices.TryGetValue(authenticationType, out authQueue))
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Authentication type not supported: {0} for AppId={1}/{2}", authenticationType, authRequest.ApplicationId, authRequest.ApplicationVersion);
                        }

                        this.OnCustomAuthenticationError(peer, authRequest, sendParameters, null, "Authentication type not supported");
                        return;
                    }

                    if (Decryptor == null)
                    {
                        try
                        {
                            InitDecryptorAndHmac(authQueue.QueryStringParametersCollection["secret1"], authQueue.QueryStringParametersCollection["secret2"], out Decryptor, out Decryptor2, out SignatureHmac, out SignatureHmac2);
                        }
                        catch (Exception ex)
                        {
                            log.WarnFormat("Custom auth JWT - exception in InitDecryptorAndHmac: {0}", ex.Message);
                        }
                    }

                    this.HandleJwtAuthentication(peer, authRequest, sendParameters, state, ClientAuthenticationType.Jwt,
                        "nA", this.Decryptor, this.Decryptor2, this.SignatureHmac, this.SignatureHmac2);
                    return;
                }

                //Xbox is still WIP
//                if (authenticationType == ClientAuthenticationType.Xbox && !Photon.Common.Authentication.Settings.Default.UseCustomAuthService)
//                {
//                    IClientAuthenticationQueue authQueue;
//                    if (this.authenticationServices.TryGetValue(authenticationType, out authQueue) == false)
//                    {
//                        // TODO log to client bug logger
//                        if (log.IsDebugEnabled)
//                        {
//                            log.DebugFormat("Authentication type not supported: {0} for AppId={1}/{2}", authenticationType,
//                                authRequest.ApplicationId, authRequest.ApplicationVersion);
//                        }
//
//                        peer.OnCustomAuthenticationError(
//                            ErrorCode.CustomAuthenticationFailed,
//                            "Authentication type not supported",
//                            authRequest,
//                            sendParameters,
//                            state);
//                        this.IncrementErrors(authenticationType, null);
//                        return;
//                    }
//                    HandleXboxAuthentication(authQueue);
//                }

                if (authenticationType == ClientAuthenticationType.PlayerIo)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("OnAuthenticateClient - PlayerIo");
                    }

                    //check if this authentication type is enabled
                    if (!this.authenticationServices.ContainsKey(ClientAuthenticationType.PlayerIo))
                    {
                        this.OnCustomAuthenticationError(peer, authRequest, sendParameters, null, "Authentication type not supported");
                        return;
                    }

                    if (PlayerIoDecryptor == null)
                    {
                        try
                        {
                            this.InitDecryptorAndHmac(Settings.Default.PlayerIoKeys, Settings.Default.PlayerIoKeys2, out PlayerIoDecryptor, out PlayerIoDecryptor2, out PlayerIoSignatureHmac, out PlayerIoSignatureHmac2);
                        }
                        catch (Exception ex)
                        {
                            if (log.IsWarnEnabled)
                            {
                                log.WarnFormat("Custom auth PlayerIO - exception in InitDecryptorAndHmac: {0}", ex.Message);
                            }
                        }
                    }

                    this.HandleJwtAuthentication(peer, authRequest, sendParameters, state, authenticationType,
                        applicationAccount.ApplicationId, this.PlayerIoDecryptor, this.PlayerIoDecryptor2, this.PlayerIoSignatureHmac, this.PlayerIoSignatureHmac2);
                    return;
                }

                base.OnAuthenticateClient(peer, authRequest, applicationAccount, sendParameters, state);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private void InitializeAuthServices(ApplicationAccount applicationAccount)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Reinitializing authentication for application: appId={0}", applicationAccount.ApplicationId);
            }

            this.isAnonymousAccessAllowed = applicationAccount.IsAnonymousAccessAllowed;

            if (applicationAccount.ClientAuthenticationServices == null || applicationAccount.ClientAuthenticationServices.Count == 0)
            {
                this.authenticationServices.Clear();
                return;
            }

            foreach (var service in applicationAccount.ClientAuthenticationServices)
            {
                service.UpdateForwardAsJSONValue();
            }

            var oldServices = this.authenticationServices;
            this.authenticationServices = new Dictionary<ClientAuthenticationType, IClientAuthenticationQueue>();

            foreach (var serviceInfo in applicationAccount.ClientAuthenticationServices)
            {
                IClientAuthenticationQueue authService;

                if (oldServices.TryGetValue(serviceInfo.AuthenticationType, out authService)
                    && serviceInfo.AuthUrl == authService.Uri
                    && serviceInfo.NameValuePairAsQueryString == authService.QueryStringParameters
                    && serviceInfo.RejectIfUnavailable == authService.RejectIfUnavailable
                    && serviceInfo.ForwardAsJSON == authService.ForwardAsJSON)
                {
                    this.authenticationServices.Add(serviceInfo.AuthenticationType, authService);
                }
                else
                {
                    var instanceName = MakeInstanceName(serviceInfo.AuthenticationType.ToString());
                    this.AddNewAuthProvider(serviceInfo.AuthUrl, serviceInfo.NameValuePairAsQueryString,
                        serviceInfo.RejectIfUnavailable, serviceInfo.AuthenticationType, serviceInfo.ForwardAsJSON, instanceName);
                }
            }
        }

        protected override void IncrementQueueFullErrors(CustomAuthResultCounters instance)
        {
            base.IncrementQueueFullErrors(instance);
            PhotonCustomAuthCounters.IncrementQueueFullErrors();
            if (this.counters != null)
            {
                this.counters.IncrementCustomAuthQueueFullErrors();
            }
        }

        protected override void IncrementQueueTimeouts(CustomAuthResultCounters instance)
        {
            base.IncrementQueueTimeouts(instance);
            PhotonCustomAuthCounters.IncrementQueueTimeouts();
            if (this.counters != null)
            {
                this.counters.IncrementCustomAuthQueueTimeouts();
            }
        }

        protected override void IncrementErrors(ClientAuthenticationType authType, CustomAuthResultCounters instance)
        {
            base.IncrementErrors(authType, instance);
            PhotonCustomAuthCounters.IncrementErrors(authType);
            if (this.counters != null)
            {
                this.counters.IncrementCustomAuthErrors(authType);
            }
        }

        protected override void IncrementResultCounters(CustomAuthenticationResult customAuthResult, CustomAuthResultCounters instance, long ticks)
        {
            base.IncrementResultCounters(customAuthResult, instance, ticks);
            switch (customAuthResult.ResultCode)
            {
                case CustomAuthenticationResultCode.Data:
                    PhotonCustomAuthCounters.IncrementResultsData();
                    if (this.counters != null)
                    {
                        this.counters.IncrementCustomAuthResultsData();
                    }
                    break;
                case CustomAuthenticationResultCode.Ok:
                    PhotonCustomAuthCounters.IncrementResultsAccepted();
                    if (this.counters != null)
                    {
                        this.counters.IncrementCustomAuthResultsAccepted();
                    }
                    break;
                default://CustomAuthenticationResultCode.Failed, CustomAuthenticationResultCode.ParameterInvalid
                    PhotonCustomAuthCounters.IncrementResultDenied();
                    if (this.counters != null)
                    {
                        this.counters.IncrementCustomAuthResultsDenied();
                    }
                    break;
            }

            PhotonCustomAuthCounters.IncrementHttpRequest(ticks);
            if (this.counters != null)
            {
                this.counters.IncrementCustomAuthHttpRequests(ticks);
            }
        }

        protected override void IncrementHttpErrors(ClientAuthenticationType authType, CustomAuthResultCounters queueCustomData)
        {
            base.IncrementHttpErrors(authType, queueCustomData);
            PhotonCustomAuthCounters.IncrementHttpErrors(authType);
            if (this.counters != null)
            {
                this.counters.IncrementCustomAuthErrors(authType);
            }
        }

        protected override void IncrementHttpTimeouts(ClientAuthenticationType authType, CustomAuthResultCounters queueCustomData)
        {
            base.IncrementHttpTimeouts(authType, queueCustomData);
            PhotonCustomAuthCounters.IncrementHttpTimeouts(authType);
            if (this.counters != null)
            {
                this.counters.IncrementCustomAuthErrors(authType);
            }
        }

        //Nintendo is private
        #region Nintendo
            
        private readonly Dictionary<string, string> NintentoEnvironments = new Dictionary<string, string>
        {
            // td1 - The environment that Nintendo uses for developing the SDK and the libraries.
            { "jd1", "https://d78dbb1c550d43c6af49bf04c56bc094-sb.baas.nintendo.com" }, //This environment is used by Nintendo to verify the compatibility of a new version of the system firmware before it is distributed to the market by testing it with released applications (with the consent of the application developers).
            { "dd1", "https://e97b8a9d672e4ce4845ec6947cd66ef6-sb.baas.nintendo.com" }, //The environment for application development and testing. By default, the Switch development hardware connects to this environment.
            { "dp1", "https://d9c8ea0e17f68bdeab8674c59f6fabda-sb.baas.nintendo.com" }, //An environment for special uses.
            { "sd1", "https://96130dc402837b377c07719e6c9514de-sb.baas.nintendo.com" }, //An environment for Lotcheck (the final testing for the retail product at Nintendo).
            { "sp1", "https://dc219b6b3aa8e06873733fda1def0e03-sb.baas.nintendo.com" }, //An environment for Lotcheck (the final testing for the retail product at Nintendo).
            { "lp1", "https://e0d67c509fb203858ebcb2fe3f88c2aa.baas.nintendo.com" }     //An environment offering services for general users.
        };


        private void NintendoAuthenticateClient(IClientAuthenticationQueue authQueue, IAuthenticateRequest authRequest, AuthQueueState queueState)
        {
            var token = "nA";
            try
            {
                var appId = authQueue.QueryStringParametersCollection["appid"];

                if (string.IsNullOrWhiteSpace(authRequest.ClientAuthenticationParams))
                {
                    NintendoFailed(authQueue, queueState, "Parameter invalid");
                    return;
                }

                var clientKeyValues = HttpUtility.ParseQueryString(authRequest.ClientAuthenticationParams);
                token = clientKeyValues["token"];

                if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(token))
                {
                    NintendoFailed(authQueue, queueState, "Parameter invalid");
                    return;
                }

                //VP: 1 - Verify that the token is in JWT format. 
                if (token.Split('.').Length != 3)
                {
                    NintendoFailed(authQueue, queueState, "Token invalid (format)");
                    return;
                }

                //in the Nintendo dashboard the app ID starts with 0x. we expect a value without leading 0x (same as in NSA ID token)
                if (appId.StartsWith("0x"))
                {
                    appId = appId.Substring(2);
                }

                //VP: 2 - Verify that the value of the alg field in Header is RS256.
                //read the headers to get alg
                var headers = JWT.Headers(token);
                var alg = (string)headers["alg"];
                if (!alg.Equals("RS256", StringComparison.InvariantCultureIgnoreCase))
                {
                    NintendoFailed(authQueue, queueState, "Token invalid (alg)");
                    return;
                }

                //VP: 3 - Verify the signature attached to the NSA ID token.
                //TODO Getting the JWKs On Demand vs Getting the JWKs Using a Periodic Batch Job
                //read the headers to get jku/kid
                var jku = (string)headers["jku"];
                var kid = (string)headers["kid"];

                //if (!jku.Contains("baas.nintendo.com"))
                if (!NintentoEnvironments.Any(c => jku.StartsWith(c.Value, StringComparison.InvariantCultureIgnoreCase)))
                {
                    NintendoFailed(authQueue, queueState, "Token invalid (jku)");
                    return;
                }

                var jsonString = JWT.Payload(token);
                JObject json = JObject.Parse(jsonString);

                //VP: 4 - Verify that the value of iss in the Claims matches the Base URL on the Nintendo authentication server.
                var iss = (string)json["iss"];
                //if (!iss.EndsWith("baas.nintendo.com"))
                if (!NintentoEnvironments.Any(c => c.Value.Equals(iss, StringComparison.InvariantCultureIgnoreCase)))
                {
                    NintendoFailed(authQueue, queueState, "Token invalid (iss)");
                    return;
                }

                //VP: 5 - Verify that the value of iat in Claims is in the past.
                var iat = (long)json["iat"];
                var iatDate = FromUnixTimeSeconds(iat);
                //You may also accept a calculation error of about 10 seconds to accept time differences among systems.
                if (iatDate > DateTime.UtcNow.AddSeconds(10))
                {
                    NintendoFailed(authQueue, queueState, "Token invalid (iat)");
                    return;
                }

                //VP: 6 - Verify that the value of exp in Claims is after the current time. 
                var exp = (long)json["exp"];
                var expDate = FromUnixTimeSeconds(exp);
                //You may also accept a calculation error of about 10 seconds to accept time differences among systems.
                if (expDate < DateTime.UtcNow.AddSeconds(-10))
                {
                    NintendoFailed(authQueue, queueState, "Token expired");
                    return;
                }

                //VP: 7 - Verify that the /nintendo/ai value in the JWT claims matches the ID of an application that is allowed to make a connection.
                var ai = (string)json["nintendo"]["ai"];
                if (!ai.Equals(appId, StringComparison.InvariantCultureIgnoreCase))
                {
                    NintendoFailed(authQueue, queueState, "Token invalid (ai)");
                    return;
                }

                //do this last, each other step can be done easily before
                //VP: 3 - Verify the signature attached to the NSA ID token.
//                GetNintendoCertificate(jku, kid, token, authQueue, queueState);
                GetNintendoCertificate(jku, kid, token, authQueue, queueState);
            }
            //TODO check which exeptions to handle separately
            //something wrong with our json handling
            catch (ArgumentException ex)
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("NintendoAuthenticateClient ArgumentException: {0}, token: '{1}'", ex.Message, token);
                }
                NintendoFailed(authQueue, queueState, "Nintendo validation failed, Token invalid");
            }
            //most likely token string has garbage data at the end resulting in an invalid signature. Should only be thrown by JWT.Decode call but just in case JWT.Headers or JWT.Payload call throws it too
            catch (FormatException ex)
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("NintendoAuthenticateClient FormatException: {0}, token: '{1}'", ex.Message, token);
                }
                NintendoFailed(authQueue, queueState, "Nintendo validation failed, token is not a valid Base-64 string (Hint: use token length parameter from Nintendo SDK and cut token)");
            }
            //other unhandled exceptions
            catch (Exception ex)
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("NintendoVerifySignature Exception: {0} ({1}), token: '{2}'", ex.Message, ex.GetType().Name, token);
                }
                NintendoFailed(authQueue, queueState, "Nintendo validation failed");
            }
        }

        private void NintendoVerifySignature(string token, AsymmetricAlgorithm key, IClientAuthenticationQueue authQueue, AuthQueueState queueState)
        {
            try
            {
                if (key == null)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("NintendoVerifySignature, key is null, token '{0}'", token);
                    }
                    NintendoFailed(authQueue, queueState, "Nintendo validation failed, Token invalid (jku/kid)");
                    return;
                }

                //VP: 3 - Verify the signature attached to the NSA ID token.
                var jsonString = JWT.Decode(token, key);

                JObject json = JObject.Parse(jsonString);

                //sub(string): Indicates the internal ID of the network service account.
                var env = NintentoEnvironments.FirstOrDefault(pair => pair.Value.Equals((string)json["iss"], StringComparison.InvariantCultureIgnoreCase));
                var userId = string.Format("{0}_{1}", json["sub"], env.Key);
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("userId: {0}", userId);
                }

                var customAuthResult = new CustomAuthenticationResult { ResultCode = ResultCodes.Ok, UserId = userId };
                this.IncrementResultCounters(customAuthResult, (CustomAuthResultCounters)authQueue.CustomData, 0);
                queueState.Peer.OnCustomAuthenticationResult(customAuthResult, queueState.AuthenticateRequest, queueState.SendParameters, queueState.State);
            }
            //TODO check which exeptions to handle separately
            //signature invalid
            catch (IntegrityException ex)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("NintendoVerifySignature IntegrityException: {0}", ex.Message);
                }
                NintendoFailed(authQueue, queueState, "Nintendo validation failed, invalid signature");
            }
            //something wrong with our json handling
            catch (ArgumentException ex)
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("NintendoVerifySignature ArgumentException: {0}, token: '{1}'", ex.Message, token);
                }
                NintendoFailed(authQueue, queueState, "Nintendo validation failed, Token invalid");
            }
            //most likely token string has garbage data at the end resulting in an invalid signature. Thrown by JWT.Decode call
            catch (FormatException ex)
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("NintendoVerifySignature FormatException: {0}, token: '{1}'", ex.Message, token);
                }
                NintendoFailed(authQueue, queueState, "Nintendo validation failed, token is not a valid Base-64 string (Hint: use token length parameter from Nintendo SDK and cut token)");
            }
            //other unhandled exceptions
            catch (Exception ex)
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("NintendoVerifySignature Exception: {0} ({1}), token: '{2}'", ex.Message, ex.GetType().Name, token);
                }
                NintendoFailed(authQueue, queueState, "Nintendo validation failed");
            }
        }

        //TODO global
        //key jku value dict key kid value key
        private Dictionary<string, NintendoCertificateJkuCache> cachedCertificatesByJku = new Dictionary<string, NintendoCertificateJkuCache>();

        private void GetNintendoCertificate(string jku, string kid, string token, IClientAuthenticationQueue authQueue, AuthQueueState queueState, int num = 0)
        {
            //simple timeout (10 sec)
            if (num >= 100)
            {
                NintendoFailed(authQueue, queueState, "Certificate retrival timeout");
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("GetNintendoCertificate, kid '{0}', jku '{1}'", kid, jku);
            }

            if (cachedCertificatesByJku.ContainsKey(jku))
            {
                //waiting for initial retrival
                if (cachedCertificatesByJku[jku].KeysByKid == null)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Waiting for initial retrival");
                    }
                    this.fiber.Schedule(() => GetNintendoCertificate(jku, kid, token, authQueue, queueState, ++num), 100);
                    return;
                }

                AsymmetricAlgorithm key = null;
                if (cachedCertificatesByJku[jku].KeysByKid.ContainsKey(kid))
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Found kid");
                    }
                    key = cachedCertificatesByJku[jku].KeysByKid[kid];
                }
                this.fiber.Enqueue(()=> NintendoVerifySignature(token, key, authQueue, queueState));
                return;
            }

            //else? we need to know if we are supposed to start retrival or wait
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Scheduling retrival jku '{0}'", jku);
            }

            var task = this.fiber.ScheduleOnInterval(() => StartGetNintendoCertificate(jku), 0, 300*1000);
            var entry = new NintendoCertificateJkuCache {Jku = jku, Task = task};
            cachedCertificatesByJku.Add(jku, entry);
            this.fiber.Schedule(() => GetNintendoCertificate(jku, kid, token, authQueue, queueState, num++), 100);
        }

        private class NintendoCertificateJkuCache
        {
            public DateTime LastUpdate;
            //remove?
            public string Jku;
            public Dictionary<string, AsymmetricAlgorithm> KeysByKid;

            public IDisposable Task;
        }

        private void StartGetNintendoCertificate(string jku)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("StartGetNintendoCertificate, getting jku '{0}'", jku);
            }

            var httpWebRequest = (HttpWebRequest)WebRequest.Create(jku);
            httpWebRequest.BeginGetResponse(EnqueueGetNintendoCertificateCallback, httpWebRequest);
        }

        private void EnqueueGetNintendoCertificateCallback(IAsyncResult asyncResult)
        {
            fiber.Enqueue(() => GetNintendoCertificateCallback(asyncResult));
        }

        //TODO we have to inform the client if we can't get the certificate
        private void GetNintendoCertificateCallback(IAsyncResult asyncResult)
        {
            var httpWebRequest = (HttpWebRequest)asyncResult.AsyncState;

            try
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("GetNintendoCertificateCallback '{0}'", httpWebRequest.RequestUri);
                }

                // End the Asynchronous response.
                var response = httpWebRequest.EndGetResponse(asyncResult);

                string result;
                using (var streamReader = new StreamReader(response.GetResponseStream()))
                {
                    result = streamReader.ReadToEnd();
                }

                var entries = new Dictionary<string, AsymmetricAlgorithm>();

               //VP: The data that can be obtained is called a JSON web key set, or JWKs, and is expressed as a JSON object array containing public keys and certificates.
                JObject d = JObject.Parse(result);
                foreach (var entry in d["keys"])
                {
                    //VP: The JWKs contains the kid property to specify which key to use.
                    //VP: The key information matching kid field in Header of the NSA ID token is obtained from the JWKs to verify the NSA ID token.
                    //VP: The signature verification algorithm is fixed to RS256 (RSA - SHA256) as indicated by the Header alg value.
                    //VP: Use the x5c value (certificate in X.509 format) contained in the JWK or the public key's n value (Modulus)/e value (Exponent) for verification.
                    var kid = (string) entry["kid"];
                    var x5c = (string)entry["x5c"][0];
                    var cert = new X509Certificate2();
                    //                cert.Import(Base64Url.Decode(x5c));
                    cert.Import(Convert.FromBase64String(x5c));
                    var key = cert.PublicKey.Key;
                    entries.Add(kid, key);

                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Adding kid '{0}'", kid);
                    }
                }

                var jku = httpWebRequest.RequestUri.ToString();
                //TODO check for key, always expected to exist
                cachedCertificatesByJku[jku].KeysByKid = entries;
            }
            //TODO log level?
            //webrequest to get certificate failed
            catch (WebException ex)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("GetNintendoCertificateCallback: {0}", ex.Message);
                }
//                NintendoFailed(nintendoRequestState.AuthQueue, nintendoRequestState.QueueState, "Nintendo validation failed, can't retrieve certificate");
            }
            //something wrong with our json handling
            catch (ArgumentException ex)
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("GetNintendoCertificateCallback: {0}", ex.Message);
                }
//                NintendoFailed(nintendoRequestState.AuthQueue, nintendoRequestState.QueueState, "Nintendo validation failed, Token invalid");
            }
            //other unhandled exceptions
            catch (Exception ex)
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("GetNintendoCertificateCallback: {0}", ex.Message);
                }
//                NintendoFailed(nintendoRequestState.AuthQueue, nintendoRequestState.QueueState, "Nintendo validation failed");
            }
        }

        private void NintendoFailed(IClientAuthenticationQueue queue, AuthQueueState queueState, string message)
        {
            var result = new CustomAuthenticationResult
            {
                ResultCode = ResultCodes.Failed,
                Message = message
            };

            this.IncrementErrors(queue.ClientAuthenticationType, (CustomAuthResultCounters)queue.CustomData);
            queueState.Peer.OnCustomAuthenticationResult(result, queueState.AuthenticateRequest, queueState.SendParameters, queueState.State);
        }

        #endregion

        //Xbox is private
        #region Xbox 

        private void HandleXboxAuthentication(IClientAuthenticationQueue authQueue)
        {
            var title = authQueue.QueryStringParametersCollection["title"];

            var customerPrivateKeyPEM = GetPrivateKey(title);




            /*
            // read title param
            title := r.URL.Query().Get("title")
            if title == "" {
                ctx.Debugf("PARAMTER_INVALID title")
                result := Result{3, "", "PARAMTER_INVALID title", "0"}
                writeResponse(w, result)
                return
            }

            // get customer private key
            customerPrivateKeyPEM := getPrivateKey(title)
            if customerPrivateKeyPEM == "" {
                result := Result{3, "", "GET_PRIVATE_KEY_ERROR", "0"}
                writeResponse(w, result)
                return
            }
            // get customer private key (pem.Decode will find the next PEM formatted block (certificate, private key etc) in the input)
            customerPrivateKeyBlock, _ := pem.Decode([]byte(customerPrivateKeyPEM))
            if customerPrivateKeyBlock == nil {
            ctx.Debugf("XSTS_PRIVATE_KEY_DECODE_ERROR 01 %s", customerPrivateKeyPEM)
            result := Result{3, "", "XSTS_PRIVATE_KEY_DECODE_ERROR", "0"}
            writeResponse(w, result)
            return
            }
            // parse customer private key (x509. returns a RSA private key from its ASN.1 PKCS#1 DER encoded form)
            customerPrivateKey, err := x509.ParsePKCS1PrivateKey(customerPrivateKeyBlock.Bytes)
            if err != nil {
            ctx.Debugf("XSTS_PRIVATE_KEY_PARSE_ERROR 01 %s", err)
            result := Result{3, "", "XSTS_PRIVATE_KEY_PARSE_ERROR", "0"}
            writeResponse(w, result)
            return
            }

            // read XSTS token from http request body
            encryptedXsts, err := ioutil.ReadAll(r.Body)
            if err != nil {
            ctx.Debugf("XSTS_READ_ERROR 01 %s", err)
            result := Result{3, "", "XSTS_READ_ERROR", "0"}
            writeResponse(w, result)
            return
            }
            // DEBUG
            //ctx.Debugf("XSTS (encrypted) %s", encryptedXsts)
            // cut off unused start of token "XBL3.0 x=11131219952558947463;ey..."
            // XBL3.0 token (Xbox One)
            encryptedXstsString := string(encryptedXsts)
            if len(encryptedXstsString) > 0 && strings.HasPrefix(encryptedXstsString, "XBL") {
            pos := strings.Index(encryptedXstsString, ";")
            encryptedXstsString = encryptedXstsString[pos+1 : len(encryptedXstsString)]
            }

            // decrypt XSTS token (JWT/JWE) with customer private key using JWE lib - (encrypted with the customer public key)
            customerPrivateKeyProvider := gojwe.ProviderFromKey(customerPrivateKey)
            decryptedXstsBytes, err := gojwe.VerifyAndDecrypt(encryptedXstsString, customerPrivateKeyProvider)
            if err != nil {
            ctx.Debugf("XSTS_DECRYPTION_ERROR 01 %s", err)
            result := Result{3, "", "XSTS_DECRYPTION_ERROR", "0"}
            writeResponse(w, result)
            return
            }

            // Xbox Live Signing certificate(s)
            const xboxlivePublicKeyPEM = `
            -----BEGIN PUBLIC KEY-----
            MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEArfJdWDt/aLiFLILH4HDa
            ZlKDmWQ6ptjzahWRoA9AYdQoeQ1WKdYSuwXaUrwiGUqi077/DUnjwUoGgHQitoRW
            gEumR63z+rweVBxOHb1PGo8+aUpqAHLQL71Kby5MNswD+fTzl3gTwFVK2mzr4SmJ
            K7mjTMgKIWd/rlHGr3ex7akXW9sTTTDKB2o5o8TwnM3s8b4Yu4LI5E6fxHM2cYIP
            bWSU3N4E/CXgLpAP5jFCWm03duD8e+AWxda1xDooUjBK7cwYpQnVA25nejrxIBm+
            4bgVpwkiPSVH4cnavs9J7z7Q7SM1KCyOjV2+MElwrEjyzTopVg9ckqjEHKI5Spv+
            2wIDAQAB
            -----END PUBLIC KEY-----`
            // parse XboxLive public key (jwt.ParseRSAPublicKeyFromPEM Parse PEM encoded PKCS1 or PKCS8 public key)
            xboxlivePublicKey, err := jwt.ParseRSAPublicKeyFromPEM([]byte(xboxlivePublicKeyPEM))
            if err != nil {
            ctx.Debugf("XSTS_PUBLIC_KEY_PARSE_ERROR 01 %s", err)

            result := Result{3, "", "XSTS_PUBLIC_KEY_PARSE_ERROR", "0"}
            writeResponse(w, result)
            return
            }

            // parse XSTS token using JWT lib with XboxLive public key verification - verifies JWT signature (signed with Xbox private key)
            // (jwt.Parse - Parse, validate, and return a token. keyFunc will receive the parsed token and should return the key for validating.)
            token, err := jwt.Parse(string(decryptedXstsBytes), func(token *jwt.Token) (interface{}, error) {
            return xboxlivePublicKey, nil
            })

            // TODO remove log
            ctx.Debugf("Token (decrypted, .Raw base64) %s, %s , %s", token.Header, token.Claims, token.Raw) // .Raw base64 encoded
            if err != nil {
            msg := ""
            if ve, ok := err.(*jwt.ValidationError); ok {
            if ve.Errors&jwt.ValidationErrorMalformed != 0 {
                msg = "malformed"
            } else if ve.Errors&(jwt.ValidationErrorExpired|jwt.ValidationErrorNotValidYet) != 0 {
                msg = "time error"
            } else {
                msg = "unknown error"
            }
            }

            ctx.Debugf("XSTS_PARSE_ERROR 01 %s - %s", err, msg)

            result := Result{3, "", "XSTS_PARSE_ERROR", "0"}
            writeResponse(w, result)
            return
            }

            // JWT
            // make sure token is valid
            // (Validates time based claims "exp, iat, nbf". There is no accounting for clock skew.
            // As well, if any of the above claims are not in the token, it will still be considered a valid claim)
            if token.Valid == false {
            ctx.Debugf("XSTS_TOKEN_NOT_VALID 01 %s %s", token.Header, token)

            result := Result{2, "", "XSTS_TOKEN_NOT_VALID", "0"}
            writeResponse(w, result)
            return
            }

            // check all xui entries for Multiplayer Privilege (254) OR (158)
            var multiplayerAllowed bool
            var gamertag string

            claims := token.Claims.(jwt.MapClaims)

            xuiArray, _ := claims["xui"].([]interface{})
            for _, xui := range xuiArray {
            privileges, _ := xui.(map[string]interface{})["prv"].(string)
            gamertagTmp, _ := xui.(map[string]interface{})["gtg"].(string)

            if strings.Contains(privileges, "254") || strings.Contains(privileges, "158") {
            multiplayerAllowed = true
            }

            gamertag = gamertagTmp
            }

            if multiplayerAllowed {
            var expireAt = claims["exp"].(float64)
            result := Result{1, gamertag, "XSTS_MULTIPLAYER_ALLOWED", strconv.FormatFloat(expireAt, 'f', 0, 64)}
            writeResponse(w, result)
            // TODO remove
            ctx.Debugf("OK %s %s %s", result.Message, result.UserId, result.ExpireAt)
            } else {
            result := Result{2, "", "XSTS_MULTIPLAYER_NOT_ALLOWED", "0"}
            writeResponse(w, result)
            // TODO remove
            ctx.Debugf("FAILED %s %s", result.Message, gamertag)
            }
            */

        }

        private string GetPrivateKey(string title)
        {
            //TODO get from Vault or similar
            return null;
        }

        #endregion


        //moved from CustomAuthHandler to remove JWT custom auth from SDK
        #region JWT

        protected void InitDecryptorAndHmac(/*string keyHash, string keyEncryption, string keySignature, string keyHash2, string keyEncryption2, string keySignature2,*/
            string secret1, string secret2, out Decryptor decryptor, out Decryptor decryptor2, out HMACSHA256 signatureHmac, out HMACSHA256 signatureHmac2)
        {
            decryptor = null;
            decryptor2 = null;
            signatureHmac = null;
            signatureHmac2 = null;

            string keyHash = null;
            string keyEncryption = null;
            string keySignature = null;

            //TODO null check
            var split = secret1.Split(';');
            //expected: [keyHash];[keyEncryption];[keySignature]
            if (split.Length < 3)
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("secret1 has invalid number of entries, expected 3, were {0}: '{1}'", split.Length, secret1);
                }
                //not sure if we want to support invalid settings (=less than 3 separate keys)
                //no signature key, use hash key instead
                if (split.Length == 2)
                {
                    keyHash = split[0];
                    keyEncryption = split[1];
                    keySignature = split[0];
                }
                //only a single value for all keys
                else if (split.Length == 1)
                {
                    keyHash = split[0];
                    keyEncryption = split[0];
                    keySignature = split[0];
                }
            }
            else
            {
                keyHash = split[0];
                keyEncryption = split[1];
                keySignature = split[2];
            }
            if (string.IsNullOrEmpty(keyHash) || string.IsNullOrEmpty(keyEncryption) || string.IsNullOrEmpty(keySignature))
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("secret1 entries are null or empty: '{0}'", secret1);
                }
                return;
            }

            string keyHash2 = null;
            string keyEncryption2 = null;
            string keySignature2 = null;

            //TODO null check
            var split2 = secret2.Split(';');
            //expected: [keyHash];[keyEncryption];[keySignature]
            if (split2.Length < 3)
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("secret2 has invalid number of entries, expected 3, were {0}: '{1}'", split2.Length, secret2);
                }
                //no signature key, use hash key instead
                if (split2.Length == 2)
                {
                    keyHash2 = split2[0];
                    keyEncryption2 = split2[1];
                    keySignature2 = split2[0];
                }
                //only a single key for all keys
                else if (split2.Length == 1)
                {
                    keyHash2 = split2[0];
                    keyEncryption2 = split2[0];
                    keySignature2 = split2[0];
                }
            }
            else
            {
                keyHash2 = split2[0];
                keyEncryption2 = split2[1];
                keySignature2 = split2[2];
            }

            byte[] secretBytesHash;
            byte[] secretBytesEncryption;
            //try second decryptor (in case values are changed)
            byte[] secretBytesHash2 = null;
            byte[] secretBytesEncryption2 = null;

            using (var hashProvider = SHA256.Create())
            {
                //use a new variable for hashes?
                secretBytesHash = hashProvider.ComputeHash(Encoding.UTF8.GetBytes(keyHash));
                secretBytesEncryption = hashProvider.ComputeHash(Encoding.UTF8.GetBytes(keyEncryption));

                //fallback is optional
                if (!string.IsNullOrEmpty(keyEncryption2) && !string.IsNullOrEmpty(keyHash2))
                {
                    secretBytesHash2 = hashProvider.ComputeHash(Encoding.UTF8.GetBytes(keyHash2));
                    secretBytesEncryption2 = hashProvider.ComputeHash(Encoding.UTF8.GetBytes(keyEncryption2));
                }
            }

            decryptor = new Decryptor();
            decryptor.Init(secretBytesEncryption, secretBytesHash);

            if (secretBytesHash2 != null && secretBytesEncryption2 != null)
            {
                decryptor2 = new Decryptor();
                decryptor2.Init(secretBytesEncryption2, secretBytesHash2);
            }

            //added signature
            signatureHmac = new HMACSHA256(Encoding.UTF8.GetBytes(keySignature));
            if (!string.IsNullOrEmpty(keySignature2))
            {
                signatureHmac2 = new HMACSHA256(Encoding.UTF8.GetBytes(keySignature2));
            }
        }

        protected void HandleJwtAuthentication(ICustomAuthPeer peer, IAuthenticateRequest authRequest, SendParameters sendParameters, object state, ClientAuthenticationType authenticationType, string appId,
            Decryptor decryptor, Decryptor decryptor2, HMACSHA256 signatureHmac, HMACSHA256 signatureHmac2)
        {
            //this should not happen
            if (decryptor == null)
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("No decryptor available for auth type {0}, appId {1}", authenticationType, appId);
                }
                this.OnCustomAuthenticationError(peer, authRequest, sendParameters, null, ErrorMessages.AuthTokenTypeNotSupported);
                return;
            }

            var usedDecryptor = decryptor;

            //additional checks required?
            if (authRequest.ClientAuthenticationData == null || authRequest.ClientAuthenticationData.GetType() != typeof(string))
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("ClientAuthenticationData invalid");
                }
                this.OnCustomAuthenticationError(peer, authRequest, sendParameters, null, ErrorMessages.AuthTokenInvalid);
                return;
            }

            byte[] eyncryptedTokenBytes;
            try
            {
                eyncryptedTokenBytes = Convert.FromBase64String((string)authRequest.ClientAuthenticationData);
            }
            catch (Exception ex)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Convert.FromBase64String failed for '{0}': {1}", authRequest.ClientAuthenticationData, ex.Message);
                }
                this.OnCustomAuthenticationError(peer, authRequest, sendParameters, null, ErrorMessages.AuthTokenInvalid);
                return;
            }

            //check HMAC
            bool validHMAC;
            //decryptor can throw exception if invalid input 
            try
            {
                validHMAC = usedDecryptor.CheckHMAC(eyncryptedTokenBytes);
            }
            catch (Exception ex)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Exception during HMAC check : {0}", ex.Message);
                }
                this.OnCustomAuthenticationError(peer, authRequest, sendParameters, null, ErrorMessages.AuthTokenInvalid);
                return;
            }

            if (!validHMAC)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("HMAC check decryptor 1 failed");
                }

                if (decryptor2 != null && decryptor2.CheckHMAC(eyncryptedTokenBytes))
                {
                    usedDecryptor = decryptor2;
                }
                else
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("HMAC check decryptor 2 failed, decrypter 2 available: {0}", decryptor2 != null);
                    }
                    this.OnCustomAuthenticationError(peer, authRequest, sendParameters, null, ErrorMessages.AuthTokenInvalid);
                    return;
                }
            }

            //decrypt token
            byte[] decrypetedTokenBytes;
            try
            {
                decrypetedTokenBytes = usedDecryptor.DecryptBufferWithIV(eyncryptedTokenBytes, 0, eyncryptedTokenBytes.Length - Encryptor.HMAC_SIZE);
            }
            catch (Exception ex)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Cannot decrypt token: {0}", ex.Message);
                }
                this.OnCustomAuthenticationError(peer, authRequest, sendParameters, null, ErrorMessages.AuthTokenInvalid);
                return;
            }
            var signedAndEncodedToken = Encoding.UTF8.GetString(decrypetedTokenBytes);

            CustomAuthJwtPayload jwtToken;
            try
            {
                jwtToken = new CustomAuthJwtPayload(signedAndEncodedToken);
            }
            catch (Exception ex)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Could not deserialize token '{0}:\n{1}'", signedAndEncodedToken, ex.Message);
                }
                this.OnCustomAuthenticationError(peer, authRequest, sendParameters, null, ErrorMessages.AuthTokenInvalid);
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("JWT Token: {0}", jwtToken);
            }

            //check the signature for completeness sake
            try
            {
                if (!jwtToken.IsSignatureValid(signatureHmac))
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Signature invalid");
                    }
                    if (signatureHmac2 == null || !jwtToken.IsSignatureValid(signatureHmac2))
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Signature 2 invalid");
                        }
                        this.OnCustomAuthenticationError(peer, authRequest, sendParameters, null, ErrorMessages.AuthTokenInvalid);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Could not create signature: {0}", ex.Message);
                }
                this.OnCustomAuthenticationError(peer, authRequest, sendParameters, null, ErrorMessages.AuthTokenInvalid);
                return;
            }

            //check AppId if PlayerIo
            if (authenticationType == ClientAuthenticationType.PlayerIo && jwtToken.AppId != appId)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Wrong AppId, expected '{0}', got '{1}'", appId, jwtToken.AppId);
                }
                this.OnCustomAuthenticationError(peer, authRequest, sendParameters, null, ErrorMessages.AuthTokenInvalid);
                return;
            }

            //check expire
            if (!jwtToken.IsValid)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Token expired");
                }
                this.OnCustomAuthenticationError(peer, authRequest, sendParameters, null, ErrorMessages.AuthTokenExpired);
                return;
            }

            var customAuthResult = new CustomAuthenticationResult
            {
                ResultCode = CustomAuthenticationResultCode.Ok, //valid token only for successfully authenticated users
                ExpireAt = jwtToken.ExpireAt,   //jwtToken.ValidTo.Ticks,
                UserId = jwtToken.UserId,
                Nickname = jwtToken.UserId,
                AuthCookie = jwtToken.AuthCookie,
            };

            CustomAuthResultCounters.IncrementResultsAccepted(null);
            peer.OnCustomAuthenticationResult(customAuthResult, authRequest, sendParameters, state);
        }

        public class CustomAuthJwtPayload
        {
            public readonly string AppId;
            public readonly string UserId;
            public readonly string Nickname;
            public readonly Dictionary<string, object> AuthCookie;

            //        public readonly DateTime ValidTo;
            public readonly long ExpireAt;

            //decoded
            private string payload;
            //encoded, token is [header].[payload]
            private string token;
            private string signature;

            public CustomAuthJwtPayload(string tokenWithSignature)
            {
                var split = tokenWithSignature.Split('.');
                if (split.Length != 3)
                {
                    throw new ArgumentException("Invalid token");
                }

                token = string.Format("{0}.{1}", split[0], split[1]);
                signature = split[2];

                payload = Encoding.UTF8.GetString(base64urldecode(split[1]));
                JwtCustomAuthData jwtCustomAuthData;
                try
                {
                    jwtCustomAuthData = JsonConvert.DeserializeObject<JwtCustomAuthData>(payload);
                }
                catch (Exception ex)
                {
                    throw new FormatException(string.Format("Cannot deserialize JwtCustomAuthData '{0}':\n{1}", payload, ex.Message));
                }

                AppId = jwtCustomAuthData.AppId;
                UserId = jwtCustomAuthData.UserId;
                Nickname = jwtCustomAuthData.Nickname;
                try
                {
                    AuthCookie = string.IsNullOrEmpty(jwtCustomAuthData.AuthCookie) ? new Dictionary<string, object>() : JsonConvert.DeserializeObject<Dictionary<string, object>>(jwtCustomAuthData.AuthCookie);
                }
                catch (Exception ex)
                {
                    throw new FormatException(string.Format("Cannot deserialize AuthCookie '{0}':\n{1}", jwtCustomAuthData.AuthCookie, ex.Message));
                }
                ExpireAt = jwtCustomAuthData.Exp;
            }

            public bool IsValid
            {
                get { return DateTime.UtcNow <= FromUnixTimeSeconds(ExpireAt).DateTime; }
            }

            //copied from DateTimeOffset, replaced with values
            private const long UnixEpochTicks = 621355968000000000L;        //TimeSpan.TicksPerDay * DateTime.DaysTo1970;
                                                                            //            private const long UnixEpochSeconds = 62135596800L;           //UnixEpochTicks / TimeSpan.TicksPerSecond;
                                                                            //            private const long UnixEpochMilliseconds = 62135596800000L;   //UnixEpochTicks / TimeSpan.TicksPerMillisecond;
            public static DateTimeOffset FromUnixTimeSeconds(long seconds)
            {
                //repaced with values
                const long MinSeconds = -62135596800L;      //DateTime.MinTicks / TimeSpan.TicksPerSecond - UnixEpochSeconds;
                const long MaxSeconds = 253402300799L;      //DateTime.MaxTicks / TimeSpan.TicksPerSecond - UnixEpochSeconds;

                if (seconds < MinSeconds || seconds > MaxSeconds)
                {
                    throw new ArgumentOutOfRangeException("seconds", string.Format("Value must be between {0} and {1}: {2}", MinSeconds, MaxSeconds, seconds));
                }

                long ticks = seconds * TimeSpan.TicksPerSecond + UnixEpochTicks;
                return new DateTimeOffset(ticks, TimeSpan.Zero);
            }

            //if we expect this to be called more than one time we should store the calculated signature the first time
            public bool IsSignatureValid(HMACSHA256 hmac)
            {
                var sig = base64urlencode(hmac.ComputeHash(Encoding.UTF8.GetBytes(token)));
                return sig == signature;
            }

            public override string ToString()
            {
                return payload;
            }

            //        private string CalculateSignature(byte[] keySignature)
            //        {
            //            string result;
            //            using (HMACSHA256 hmac = new HMACSHA256(keySignature))
            //            {
            //                var sigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(token));
            //                result = base64urlencode(sigBytes);
            //            }
            //
            //            return result;
            //        }

            #region from https://tools.ietf.org/html/draft-ietf-jose-json-web-signature-08#appendix-C
            static string base64urlencode(byte[] arg)
            {
                string s = Convert.ToBase64String(arg); // Regular base64 encoder
                s = s.Split('=')[0]; // Remove any trailing '='s
                s = s.Replace('+', '-'); // 62nd char of encoding
                s = s.Replace('/', '_'); // 63rd char of encoding
                return s;
            }

            static byte[] base64urldecode(string arg)
            {
                string s = arg;
                s = s.Replace('-', '+'); // 62nd char of encoding
                s = s.Replace('_', '/'); // 63rd char of encoding
                switch (s.Length % 4) // Pad with trailing '='s
                {
                    case 0: break; // No pad chars in this case
                    case 2: s += "=="; break; // Two pad chars
                    case 3: s += "="; break; // One pad char
                    default:
                        throw new System.Exception("Illegal base64url string!");
                }
                return Convert.FromBase64String(s); // Standard base64 decoder
            }
            #endregion
        }

        //required payload for custom auth as defined
        public class JwtCustomAuthData
        {
            //Photon
            public string AppId { get; set; }
            public string UserId { get; set; }
            public string Nickname { get; set; }
            public string AuthCookie { get; set; }

            //Jwt
            public long Exp { get; set; }
            public long Iat { get; set; }
            public long Nbf { get; set; }
        }

        #endregion
    }
}
