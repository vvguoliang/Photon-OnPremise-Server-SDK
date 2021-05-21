// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AccountServiceAuthentication.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;
using ExitGames.Threading;
using Photon.Common.Authentication;
using Photon.SocketServer.Diagnostics;
using Photon.SocketServer.Net;

namespace PhotonCloud.NameServer.Monitoring
{
    public class MonitoringService
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly string monitoringServiceUrl;
//        private readonly string accountServiceUrlWithCacheRefresh;

//        private readonly string accountServiceUserName;
//
//        private readonly string accountServicePassword;

//        private readonly string blobServiceUrl;
//        private readonly string fallBackBlobServiceUrl;

        private readonly int requestTimeout;

//        private readonly AccountServiceHealthController healthController;

//        private readonly HttpRequestQueue blobHttpQueue = new HttpRequestQueue(
//                new PoolFiber(new BeforeAfterExecutor(
//                () =>
//                {
//                    log4net.ThreadContext.Properties["AppId"] = "Global:AccountService";
//                },
//                () => log4net.ThreadContext.Properties.Clear()))
//            );

//        private readonly HttpRequestQueue fallBackBlobHttpQueue = new HttpRequestQueue(
//            new PoolFiber(new BeforeAfterExecutor(
//                () =>
//                {
//                    log4net.ThreadContext.Properties["AppId"] = "Global:AccountService";
//                },
//                () => log4net.ThreadContext.Properties.Clear()))
//        );

        private readonly HttpRequestQueue monitoringServiceHttpQueue = new HttpRequestQueue(
                new PoolFiber(new BeforeAfterExecutor(
                () =>
                {
                    log4net.ThreadContext.Properties["AppId"] = "Global:MonitoringService";
                },
                () => log4net.ThreadContext.Properties.Clear()))
            );

        private readonly LogCountGuard monitoringServiceHttpQueueLogGuard = new LogCountGuard(new TimeSpan(0, 0, 30), 10);
//        private readonly LogCountGuard blobHttpQueueLogGuard = new LogCountGuard(new TimeSpan(0, 0, 30), 10);

        #endregion

        #region Constructors

        public MonitoringService(string monitoringServiceUrl, /*string accountServiceUrlWithCacheRefresh,
            string accountServiceUserName, string accountServicePassword,
            string blobServiceUrl, string fallBackBlobServiceUrl,*/ int requestTimeOut/*, IHealthMonitor healthMonitor*/)
        {
            this.monitoringServiceUrl = monitoringServiceUrl;
//            this.accountServiceUrlWithCacheRefresh = accountServiceUrlWithCacheRefresh;
//            this.accountServiceUserName = accountServiceUserName;
//            this.accountServicePassword = accountServicePassword;
//            this.blobServiceUrl = blobServiceUrl;
//            this.fallBackBlobServiceUrl = fallBackBlobServiceUrl;


            this.requestTimeout = requestTimeOut;
            this.Timeout = requestTimeOut;
//            if (!string.IsNullOrEmpty(blobServiceUrl))
//            {
//                this.Timeout += requestTimeOut;
//            }

//            this.blobHttpQueue.QueueTimeout = TimeSpan.FromMilliseconds(2 * this.requestTimeout);
//            this.fallBackBlobHttpQueue.QueueTimeout = TimeSpan.FromMilliseconds(2 * this.requestTimeout);
            this.monitoringServiceHttpQueue.QueueTimeout = TimeSpan.FromMilliseconds(2 * this.requestTimeout);

//            this.healthController = new AccountServiceHealthController(this.blobHttpQueue, this.accountServiceHttpQueue);
//            if (healthMonitor != null)
//            {
//                healthMonitor.AddController(this.healthController);
//            }
        }

        #endregion

        #region Properties

        public int Timeout { get; private set; }

//        public int BlobMaxConcurentRequests
//        {
//            get { return this.blobHttpQueue.MaxConcurrentRequests; }
//            set
//            {
//                this.blobHttpQueue.MaxConcurrentRequests = value;
//                this.fallBackBlobHttpQueue.MaxConcurrentRequests = value;
//            }
//        }
//
//        public int BlobMaxQueuedRequests
//        {
//            get { return this.blobHttpQueue.MaxQueuedRequests; }
//            set
//            {
//                this.blobHttpQueue.MaxQueuedRequests = value;
//                this.fallBackBlobHttpQueue.MaxQueuedRequests = value;
//            }
//        }
//
//        public int BlobMaxErrorRequests
//        {
//            get { return this.blobHttpQueue.MaxErrorRequests; }
//            set
//            {
//                this.blobHttpQueue.MaxErrorRequests = value;
//                this.fallBackBlobHttpQueue.MaxErrorRequests = value;
//            }
//        }
//
//        public int BlobReconnectInterval
//        {
//            get { return (int)this.blobHttpQueue.ReconnectInterval.TotalSeconds; }
//            set
//            {
//                this.blobHttpQueue.ReconnectInterval = TimeSpan.FromSeconds(value);
//                this.fallBackBlobHttpQueue.ReconnectInterval = TimeSpan.FromSeconds(value);
//            }
//        }
//
//        public int[] BlobExpectedErrors
//        {
//            get { return this.blobHttpQueue.ExpectedErrorCodes; }
//            set
//            {
//                this.blobHttpQueue.ExpectedErrorCodes = value;
//                this.fallBackBlobHttpQueue.ExpectedErrorCodes = value;
//            }
//        }

        public int MonitoringServiceMaxConcurentRequests
        {
            get { return this.monitoringServiceHttpQueue.MaxConcurrentRequests; }
            set { this.monitoringServiceHttpQueue.MaxConcurrentRequests = value; }
        }

        public int MonitoringServiceMaxQueuedRequests
        {
            get { return this.monitoringServiceHttpQueue.MaxQueuedRequests; }
            set { this.monitoringServiceHttpQueue.MaxQueuedRequests = value; }
        }

        public int MonitoringServiceMaxErrorRequests
        {
            get { return this.monitoringServiceHttpQueue.MaxErrorRequests; }
            set { this.monitoringServiceHttpQueue.MaxErrorRequests = value; }
        }

        public int MonitoringServiceReconnectInterval
        {
            get { return (int)this.monitoringServiceHttpQueue.ReconnectInterval.TotalSeconds; }
            set { this.monitoringServiceHttpQueue.ReconnectInterval = TimeSpan.FromSeconds(value); }
        }

        #endregion

        #region Public Methods

        public static bool IsWellFormedUriString(string url, out string message)
        {
            if (string.IsNullOrEmpty(url))
            {
                message = "empty url specified";
                return false;
            }

            if (url.Contains("{0}") == false)
            {
                message = "Servernames placeholder is missing in url";
                return false;
            }

            var temp = string.Format(url, "TestApp");
            if (!Uri.IsWellFormedUriString(temp, UriKind.Absolute))
            {
                message = "Url is not well formed";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public bool FormatApplicationId(string applicationId, out string formattedAppId)
        {
            Guid appGuid;
            if (!Guid.TryParse(applicationId, out appGuid))
            {
                formattedAppId = string.Empty;
                return false;
            }

            formattedAppId = appGuid.ToString().ToLower();
            return true;
        }

        public void VerifyMonitoringResult(string applicationId, string servernames, Action<MonitoringResult, object> callback, object state)
        {
            Exception exception;
            try
            {
                Guid id;
                if (!Guid.TryParse(applicationId, out id))
                {
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Invalid Application ID format: {0}", applicationId);
                    }

//                    callback(new ApplicationAccount(string.Empty, AccountServiceResult.Ok, false, ErrorMessages.InvalidAppIdFormat));
                    callback(new MonitoringResult(null, MonitoringServiceResult.Ok, null, "Invalid AppId"), state);
                    return;
                }

                this.TryGetMonitoringResultAsync(id, servernames, requestState => OnGetMonitorResultForVerifing(requestState, callback, state));
                return;

            }
            catch (Exception ex)
            {
                var msg = string.Format("Monitoring - Exception during appId validation: {0}", applicationId);
                log.Error(msg, ex);
                exception = ex;
            }

            callback(new MonitoringResult(applicationId, MonitoringServiceResult.Error, null, exception.Message), state);
        }

//        public void TryGetMonitoringResult(string application, Action<MonitoringServiceResult, ExternalApiInfoList> callback)
//        {
//            try
//            {
//                if (callback == null)
//                {
//                    throw new ArgumentNullException("callback");
//                }
//
//                if (string.IsNullOrEmpty(application))
//                {
//                    throw new ArgumentException("'application' can not be null or empty string");
//                }
//
//                Guid id;
//                if (Guid.TryParse(application, out id) == false)
//                {
//                    log.WarnFormat("TryGetExternalApiInfo: Invalid Application ID format: {0}. No external API info loaded for app ID {1}.", application, id);
//                    callback(MonitoringServiceResult.Error, null);
//                    return;
//                }
//
//                this.TryGetMonitoringResultAsync(id, asyncRequestState => OnGetMonitoringResult(asyncRequestState, callback));
//                return;
//            }
//            catch (Exception ex)
//            {
//                log.Error(ex);
//            }
//
//            log.WarnFormat("AccountService.GetApplication: No external API info loaded for app ID {0}.", application);
//            callback(MonitoringServiceResult.Error, null);
//        }

        #endregion

        #region Private Methods

        #region Async Mode Code

        private static void OnGetMonitorResultForVerifing(AsyncRequestState asyncRequestState, Action<MonitoringResult, object> callback, object state)
        {
            MonitoringResult result;
            var appId = asyncRequestState.AppId.ToString();

            try
            {
                switch (asyncRequestState.ResultCode)
                {
                    case MonitoringServiceResult.Ok:
//                        account = new ApplicationAccount(appId, asyncRequestState.ResultCode, asyncRequestState.RequestResult);
                        result = new MonitoringResult(appId, asyncRequestState.ResultCode, asyncRequestState.RequestResult, null);
                        break;
                    case MonitoringServiceResult.NotFound:
                        //                        account = new ApplicationAccount(appId, asyncRequestState.ResultCode, false, ErrorMessages.InvalidAppId);
                        result = new MonitoringResult(appId, asyncRequestState.ResultCode, null, "Invalid AppId");
                        break;
                    default:
                        //                        account = new ApplicationAccount(appId, asyncRequestState.ResultCode, allowOnFailure, asyncRequestState.ErrorMessage);
                        result = new MonitoringResult(appId, asyncRequestState.ResultCode, null, asyncRequestState.ErrorMessage);
                        break;
                }
            }
            catch (Exception e)
            {
                log.Warn(e);
                //                account = new ApplicationAccount(appId, AccountServiceResult.Error, allowOnFailure, e.Message);
                result = new MonitoringResult(appId, MonitoringServiceResult.Error, null, e.Message);
            }
            callback(result, state);
        }

//        private static void OnGetMonitoringResult(AsyncRequestState asyncRequestState,
//            Action<MonitoringServiceResult, ExternalApiInfoList> callback)
//        {
//            ExternalApiInfoList apiList = null;
//            if (asyncRequestState.ResultCode == MonitoringServiceResult.Ok)
//            {
//                apiList = asyncRequestState.RequestResult.ExternalApiInfoList ?? new ExternalApiInfoList();
//            }
//            callback(asyncRequestState.ResultCode, apiList);
//        }

        private void TryGetMonitoringResultAsync(Guid appId, string servernames, Action<AsyncRequestState> callback)
        {
//            if (!string.IsNullOrEmpty(this.blobServiceUrl))
//            {
//                this.TryGetApplicationAccountAsync(this.blobHttpQueue,
//                    appId,
//                    this.blobServiceUrl,
//                    null,
//                    null,
//                    asyncRequestState => this.OnGetAccountInfoFromBlobStorage(asyncRequestState, callback));
//            }
//            else
//            {
                this.TryGetMonitoringResultAsync(this.monitoringServiceHttpQueue,
                    appId,
                    servernames,
                    this.monitoringServiceUrl,
//                    this.accountServiceUserName,
//                    this.accountServicePassword,
                    asyncRequestState => OnGetMonitoringResult(asyncRequestState, callback));
//            }
        }

        private void TryGetMonitoringResultAsync(HttpRequestQueue httpQueue, Guid appId, string servernames, string url, /*string username,
            string password,*/ Action<AsyncRequestState> onGetMonitoringInfoCallback)
        {

            var logGuard = /*httpQueue == this.blobHttpQueue ? this.blobHttpQueueLogGuard :*/ this.monitoringServiceHttpQueueLogGuard;
            var asyncRequestState = new AsyncRequestState(appId, httpQueue,
                onGetMonitoringInfoCallback,
                /*httpQueue == this.blobHttpQueue,*/
                logGuard);

            //TODO
            var uri = string.Format(url, servernames);
            var request = this.GetWebRequest(uri, /*username, password,*/ logGuard);

            const int RETRY_COUNT = 3;

            httpQueue.Enqueue(request, HttpRequestQueueCallbackAsync, asyncRequestState, RETRY_COUNT);
        }

//        private void OnGetAccountInfoFromBlobStorage(AsyncRequestState asyncRequestState,
//            Action<AsyncRequestState> callback)
//        {
//            Counter.IncrementBlobServiceRequests(asyncRequestState.Stopwatch.ElapsedTicks);
//
////            this.healthController.OnGetResponseFromBlobstore(asyncRequestState);
//
//            var triggerBlobStorageCacheRefresh = false;
//            switch (asyncRequestState.ResultCode)
//            {
//                case AccountServiceResult.Ok:
//                    callback(asyncRequestState);
//                    return;
//
//                case AccountServiceResult.NotFound:
//                    triggerBlobStorageCacheRefresh = true;
//                    Counter.IncrementBlobServiceCacheMisses();
//                    break;
//
//                case AccountServiceResult.Timeout:
//                    Counter.IncrementBlobServiceTimeouts();
//                    break;
//
//                case AccountServiceResult.Error:
//                    Counter.IncrementBlobServiceErrors();
//                    break;
//            }
//
//            if (asyncRequestState.ResultCode != AccountServiceResult.NotFound && !string.IsNullOrEmpty(this.fallBackBlobServiceUrl))
//            {
//                CallToFallbackQueue(asyncRequestState, callback);
//                return;
//            }
//
//            this.CallToAccountServiceQueue(asyncRequestState, callback, triggerBlobStorageCacheRefresh);
//        }

//        private void OnGetAccountInfoFromFallbackBlobStorage(AsyncRequestState asyncRequestState, Action<AsyncRequestState> callback)
//        {
//            if (log.IsDebugEnabled)
//            {
//                log.DebugFormat("Got response from fallback storage. Result:{0}, appId: {1}", asyncRequestState.ResultCode, asyncRequestState.AppId);
//            }
//
//            Counter.IncrementFallbackBlobServiceRequests(asyncRequestState.Stopwatch.ElapsedTicks);
//
////            this.healthController.OnGetResponseFromBlobstore(asyncRequestState);
//
//            var triggerBlobStorageCacheRefresh = false;
//            switch (asyncRequestState.ResultCode)
//            {
//                case AccountServiceResult.Ok:
//                    callback(asyncRequestState);
//                    return;
//
//                case AccountServiceResult.NotFound:
//                    triggerBlobStorageCacheRefresh = true;
//                    Counter.IncrementFallbackBlobServiceCacheMisses();
//                    break;
//
//                case AccountServiceResult.Timeout:
//                    Counter.IncrementFallbackBlobServiceTimeouts();
//                    break;
//
//                case AccountServiceResult.Error:
//                    Counter.IncrementFallbackBlobServiceErrors();
//                    break;
//            }
//
//            this.CallToAccountServiceQueue(asyncRequestState, callback, triggerBlobStorageCacheRefresh);
//        }

//        private void CallToFallbackQueue(AsyncRequestState asyncRequestState, Action<AsyncRequestState> callback)
//        {
//            try
//            {
//                if (log.IsDebugEnabled)
//                {
//                    log.DebugFormat("Using fallback storage for appId: {0}, url:{1}", asyncRequestState.AppId, this.fallBackBlobServiceUrl);
//                }
//
//                this.TryGetApplicationAccountAsync(this.fallBackBlobHttpQueue,
//                    asyncRequestState.AppId,
//                    this.fallBackBlobServiceUrl,
//                    null,
//                    null,
//                    ars => this.OnGetAccountInfoFromFallbackBlobStorage(ars, callback));
//            }
//            catch (Exception e)
//            {
//                var msg = string.Format("Account service: Exception during execution of request to account service. appId:{0}", asyncRequestState.AppId);
//                log.Error(msg, e);
//                // we call user callback with result of call to blob storage
//                callback(asyncRequestState);
//            }
//        }

//        private void CallToMonitoringServiceQueue(AsyncRequestState asyncRequestState, Action<AsyncRequestState> callback, bool triggerBlobStorageCacheRefresh)
//        {
//            try
//            {
//                var urlToUse = /*triggerBlobStorageCacheRefresh ? this.accountServiceUrlWithCacheRefresh :*/ this.accountServiceUrl;
//                if (log.IsDebugEnabled)
//                {
//                    log.DebugFormat("Call to account service for appId: {0}, UpdateCaches:{1}, url:{2}",
//                        asyncRequestState.AppId, triggerBlobStorageCacheRefresh, urlToUse);
//                }
//
//                this.TryGetMonitoringResultAsync(this.accountServiceHttpQueue,
//                    asyncRequestState.AppId, urlToUse, /*this.accountServiceUserName, this.accountServicePassword,*/
//                    requestState => this.OnGetMonitoringResult(requestState, callback));
//            }
//            catch (Exception e)
//            {
//                var msg = string.Format("Account service: Exception during execution of request to account service. appId:{0}", asyncRequestState.AppId);
//                log.Error(msg, e);
//                // we call user callback with result of call to blob storage
//                callback(asyncRequestState);
//            }
//        }

        private void OnGetMonitoringResult(AsyncRequestState asyncRequestState,
            Action<AsyncRequestState> callback)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Got response from monitoring service. Result:{0}, appId: {1}", asyncRequestState.ResultCode, asyncRequestState.AppId);
            }

            try
            {
                //TODO counter?
//                Counter.IncrementAccountServiceRequests(asyncRequestState.Stopwatch.ElapsedTicks);

//                this.healthController.OnGetResponseFromAccountService(asyncRequestState);

                switch (asyncRequestState.ResultCode)
                {
                    case MonitoringServiceResult.Ok:
                        break;

                    case MonitoringServiceResult.Timeout:
                        //TODO counter?
//                        Counter.IncrementAccountServiceTimeouts();
                        break;

                    default:
                        //TODO counter?
//                        Counter.IncrementAccountServiceServiceErrors();
                        break;
                }
            }
            catch (Exception e)
            {
                log.Warn(e);
            }
            callback(asyncRequestState);
        }

        internal class AsyncRequestState
        {
            public readonly Stopwatch Stopwatch = System.Diagnostics.Stopwatch.StartNew();
            public Guid AppId { get; private set; }
            public HttpRequestQueue HttpQueue { get; private set; }
            public Action<AsyncRequestState> Callback { get; private set; }

            public MonitoringServiceResult ResultCode { get; set; }
            public string ErrorMessage { get; set; }
            //TODO?
//            public MonitoringResult RequestResult { get; set; }
            public string RequestResult { get; set; }
            public HttpRequestQueueResultCode HttpRequestQueueResultCode { get; set; }

//            public bool IsBlobStoreQueue { get; private set; }

            public LogCountGuard LogGuard { get; private set; }

            public AsyncRequestState(Guid appId, HttpRequestQueue httpQueue,
                Action<AsyncRequestState> callback, /*bool isBlobStoreQueue,*/ LogCountGuard logGuard)
            {
                this.Callback = callback;
//                this.IsBlobStoreQueue = isBlobStoreQueue;
                this.LogGuard = logGuard;
                this.HttpQueue = httpQueue;
                this.AppId = appId;
            }
        }

        private static void HttpRequestQueueCallbackAsync(HttpRequestQueueResultCode result, AsyncHttpRequest request, object userState)
        {
            var state = (AsyncRequestState)userState;
            state.Stopwatch.Stop();
            var appId = state.AppId;
            var elapsedMS = state.Stopwatch.ElapsedMilliseconds;
            var queue = state.HttpQueue;
            state.ResultCode = MonitoringServiceResult.Error;
            state.HttpRequestQueueResultCode = result;

            switch (result)
            {
                case HttpRequestQueueResultCode.Success:
                    {
                        GetTmpMonitoringFromResponse(state, request.WebRequest.RequestUri.ToString(), Encoding.UTF8.GetString(request.Response));
                        break;
                    }
                case HttpRequestQueueResultCode.RequestTimeout:
                    {
                        if (log.IsWarnEnabled)
                        {
                            log.WarnFormat(
                                "Monitoring service: Request failed. Timeout. appId={0}, url={1}, duration: {2}ms, duration2: {4}, request timeout set to: {3}ms, ConnectionLimit:{5}",
                                appId,
                                request.WebRequest.RequestUri,
                                elapsedMS,
                                request.WebRequest.Timeout,
                                request.Elapsedtime,
                                System.Net.ServicePointManager.DefaultConnectionLimit);
                        }


                        state.ResultCode = MonitoringServiceResult.Timeout;
                        state.ErrorMessage = ErrorMessages.RequestTimedout;
                        break;
                    }
                case HttpRequestQueueResultCode.Error:
                    {
                        var response = request.WebResponse;
                        if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                        {
//                            if (state.IsBlobStoreQueue && log.IsWarnEnabled)
//                            {
//                                log.WarnFormat(state.LogGuard,
//                                    "Account service: Application account does not exist or Url is wrong - did user submit an invalid App ID? appId={0}, url={1}, duration: {2}ms, request timeout set to: {3}ms",
//                                    appId,
//                                    request.WebRequest.RequestUri,
//                                    elapsedMS,
//                                    request.WebRequest.Timeout);
//                            }

                            state.ResultCode = MonitoringServiceResult.NotFound;
                            state.ErrorMessage = ErrorMessages.AppIdNotFound;
                        }
                        else
                        {
                            var msg = string.Format("Monitoring service: Error getting application account: appId={0}, url={1}", appId, request.WebRequest.RequestUri);
                            log.Error(state.LogGuard, msg, request.Exception);
                            state.ErrorMessage = string.Format("Error during getting monitoring: WebStatus:{0}, Request Status:{1}", request.WebStatus, request.Status);
                        }
                        break;
                    }
                case HttpRequestQueueResultCode.QueueTimeout:
                    {
                        if (log.IsWarnEnabled)
                        {
                            log.WarnFormat(state.LogGuard,
                                "Monitoring service: Request failed. Http Queue Timeout. appId:'{0}', url:'{1}', timeout:{2}, max concurent request:{3}",
                                appId,
                                request.WebRequest.RequestUri,
                                queue.QueueTimeout,
                                queue.MaxConcurrentRequests
                                );
                        }

                        state.ErrorMessage = ErrorMessages.QueueTimeout;
                        break;
                    }
                case HttpRequestQueueResultCode.QueueFull:
                    {
                        if (log.IsWarnEnabled)
                        {
                            log.WarnFormat(state.LogGuard,
                                "Monitoring service: Request failed. Http Queue Full. appId:'{0}', url:'{1}', max queue size:{2}",
                                appId,
                                request.WebRequest.RequestUri,
                                queue.MaxQueuedRequests);
                        }
                        state.ErrorMessage = ErrorMessages.QueueFull;
                        break;
                    }
                case HttpRequestQueueResultCode.Offline:
                    {
                        if (log.IsWarnEnabled)
                        {
                            log.WarnFormat(state.LogGuard,
                                "Monitoring service: Request failed. Http Queue is offline. appId:'{0}', url:'{1}', queue reconnect interval:{2}",
                                appId,
                                request.WebRequest.RequestUri,
                                queue.ReconnectInterval);
                        }
                        state.ErrorMessage = ErrorMessages.QueueOffline;
                        break;
                    }
                default:
                    log.ErrorFormat("Monitoring service: Unknown result code: {0}, a", result);
                    break;
            }

            request.Dispose();

            try
            {
                state.Callback(state);
            }
            catch (Exception e)
            {
                log.Warn(state.LogGuard, string.Format("Monitoring service: Got exception during callback call. appId:{0}. Exception Msg:{1}", appId, e.Message), e);
            }
        }

        private static void GetTmpMonitoringFromResponse(AsyncRequestState requestState, string uri, string jsonResult)
        {
            //TODO?
//            try
//            {
//                var tmpApplicationAccount = JsonConvert.DeserializeObject<TmpApplicationAccount>(jsonResult);
//                if (tmpApplicationAccount == null)
//                {
//                    log.ErrorFormat("Account service: Error while deserializing application account: appId={0}, url={1}, result='{2}'",
//                        requestState.AppId, uri, jsonResult);
//                }
//                else
//                {
//                    if (log.IsDebugEnabled)
//                    {
//                        LogApplicationAccount("GetApplicationInfo: ", requestState.AppId, tmpApplicationAccount, requestState.Stopwatch.ElapsedMilliseconds);
//                    }

                    requestState.ResultCode = MonitoringServiceResult.Ok;
                    requestState.RequestResult = jsonResult;
//                }
//            }
//            catch (Exception e)
//            {
//                log.Error(string.Format("Account service: Exception while deserializing application account: appId={0}, url={1}, result={2}, Exception Msg:{3}",
//                    requestState.AppId, uri, jsonResult, e.Message), e);
//            }
        }

        #endregion

//        private static void LogApplicationAccount(string prefix, Guid id, TmpApplicationAccount account, long elapsedMilliseconds)
//        {
//            log.DebugFormat(
//                "{0} appID {1} - Result: {2} ({3}), CCUs: {4}, CCU Burst: {5}, Region: {6}, Cloud: {7}, Region/Cluster: {11}, Custom Auth Services: {8}, External APIs: {9}, (took {10} ms)",
//                prefix,
//                id,
//                account.ReturnCode,
//                account.Message,
//                account.ApplicationCcu,
//                account.ApplicationCcuBurst,
//                account.ApplicationRegion,
//                account.PrivateCloud,
//                account.ClientAuthenticationServiceInfoList == null ? 0 : account.ClientAuthenticationServiceInfoList.TotalRecordCount,
//                account.ExternalApiInfoList == null ? 0 : account.ExternalApiInfoList.TotalRecordCount,
//                elapsedMilliseconds,
//                account.RegionClusterInfo);
//        }

        private HttpWebRequest GetWebRequest(string address, /*string username, string password,*/ LogCountGuard logGuard)
        {
            try
            {
                var request = HttpWebRequest.Create(address);

                request.Proxy = null;
                request.Timeout = this.requestTimeout;

                /*if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    var authInfo = string.Format("{0}:{1}", username, password);
                    authInfo = Convert.ToBase64String(Encoding.UTF8.GetBytes(authInfo));
                    request.Headers["Authorization"] = "Basic " + authInfo;
                }*/

                return (HttpWebRequest)request;
            }
            catch (Exception e)
            {
                log.ErrorFormat(logGuard, "Monitoring service: Exception during Web Request creation. url:{0}, username:{1}, msg:{2}",
                    address, "nA", e.Message);
                throw;
            }
        }

        #endregion
    }
}
