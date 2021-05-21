// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AuthenticationHandlerFactory.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the AuthenticationHandlerFactory type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Photon.Cloud.Common.Diagnostic.HealthCheck;

namespace PhotonCloud.Authentication.AccountService
{
    using ExitGames.Logging;

    public class AccountServiceFactory
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        
        public static IAccountService GetAuthenticationHandler(IHealthMonitor healthMonitor)
        {
            int timeout = Settings.Default.AccountServiceTimeout;
            if (timeout <= 0)
            {
                log.ErrorFormat("Invalid timeout {0} specified. DefaultAuthentication will be used.", timeout);
                return new DefaultAccountService(timeout);
            }

            var accountServiceURL = Settings.Default.AccountServiceUrl;
            if (string.IsNullOrEmpty(accountServiceURL))
            {
                log.Warn("Authentication handler is not set. DefaultAuthenticaton will be used.");
                return new DefaultAccountService(timeout);
            }
            
            string message;
            if (AccountService.IsWellFormedUriString(accountServiceURL, out message) == false)
            {
                log.ErrorFormat("Invalid AccountService URL specified: message={0}, url={1}. DefaultAuthentication will be used.", message, accountServiceURL);
                return new DefaultAccountService(timeout);
            }

            var accountServiceURLWithBlobStorageCacheRefresh = GetAccountServiceUrlWithBlobStorageCacheRefresh(accountServiceURL);
            if (AccountService.IsWellFormedUriString(accountServiceURL, out message) == false)
            {
                log.ErrorFormat("Invalid AccountService URL for BlobStorageCacheRefresh specified: message={0}, url={1}. DefaultAuthentication will be used.", message, accountServiceURLWithBlobStorageCacheRefresh);
                return new DefaultAccountService(timeout);
            }
           
            var blobStorageUrl = Settings.Default.BlobServiceUrl;
            if (string.IsNullOrEmpty(blobStorageUrl))
            {
                log.InfoFormat("No authentication blob service URL specified.");
            }
            else
            {
                if (AccountService.IsWellFormedUriString(blobStorageUrl, out message) == false)
                {
                    log.ErrorFormat("Invalid BlobStorage URL specified: message={0}, URL={1}. BlobService will not be used.", message, blobStorageUrl);
                    blobStorageUrl = null;
                }
                else
                {
                    log.InfoFormat("BlobStorage will be used: URL={0}", blobStorageUrl);
                }
            }

            var fallbackBlobStorageUrl = Settings.Default.FallbackBlobServiceUrl;
            if (string.IsNullOrEmpty(fallbackBlobStorageUrl))
            {
                log.InfoFormat("No authentication fallback blob service URL specified.");
            }
            else
            {
                if (AccountService.IsWellFormedUriString(fallbackBlobStorageUrl, out message) == false)
                {
                    log.ErrorFormat("Invalid Fallback BlobStorage URL specified: message={0}, URL={1}. Fallback BlobService will not be used.", message, fallbackBlobStorageUrl);
                    fallbackBlobStorageUrl = null;
                }
                else
                {
                    log.InfoFormat("FallbackBlobStorage will be used: URL={0}", fallbackBlobStorageUrl);
                }
            }

            if (string.IsNullOrEmpty(Settings.Default.AccountServiceUsername) || string.IsNullOrEmpty(Settings.Default.AccountServicePassword))
            {
                log.ErrorFormat("No AccountService username / password specified");
            }
            
            log.InfoFormat("Authentication handler is set to {0} with timeout {1}", accountServiceURL, timeout);
            return new AccountService(accountServiceURL, accountServiceURLWithBlobStorageCacheRefresh, Settings.Default.AccountServiceUsername,
                Settings.Default.AccountServicePassword, blobStorageUrl, fallbackBlobStorageUrl, timeout, healthMonitor)
            {
                BlobMaxQueuedRequests = Settings.Default.BlobServiceMaxQueuedRequests,
                BlobMaxConcurentRequests = Settings.Default.BlobServiceMaxConcurrentRequests,
                BlobMaxErrorRequests = Settings.Default.BlobServiceMaxErrorRequests,
                BlobMaxTimedOutRequests = Settings.Default.BlobServiceMaxTimedOutRequests,
                BlobReconnectInterval = Settings.Default.BlobServiceRetryIntervalInSeconds,
                BlobExpectedErrors = GetIntArrayFromString(Settings.Default.BlobServiceExpectedErrors),

                AccountServiceMaxQueuedRequests = Settings.Default.AccountServiceMaxQueuedRequests,
                AccountServiceMaxConcurentRequests = Settings.Default.AccountServiceMaxConcurrentRequests,
                AccountServiceMaxErrorRequests = Settings.Default.AccountServiceMaxErrorRequests,
                AccountServiceMaxTimedOutRequests = Settings.Default.AccountServiceMaxTimedOutRequests,
                AccountServiceReconnectInterval = Settings.Default.AccountServiceRetryIntervalInSeconds,
            };
        }

        private static int[] GetIntArrayFromString(string blobServiceExpectedErrors)
        {
            var strArray = blobServiceExpectedErrors.Split(',');

            var list = new List<int>();
            foreach (var str in strArray)
            {
                int result;
                if (!string.IsNullOrWhiteSpace(str) && int.TryParse(str, out result))
                {
                    list.Add(result);
                }
            }
            return list.ToArray();
        }


        private static string GetAccountServiceUrlWithBlobStorageCacheRefresh(string serviceUrl)
        {
            // check if pushQueue parameter is set
            if (serviceUrl.IndexOf("pushQueue=please", System.StringComparison.InvariantCultureIgnoreCase) < 0)
            {
                if (serviceUrl.IndexOf('?') > 0)
                {

                    serviceUrl = serviceUrl + "&pushQueue=please";
                }
                else
                {

                    serviceUrl = serviceUrl + "?pushQueue=please";
                }
            }
            return serviceUrl;
        }
    }
}
