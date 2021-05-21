// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ClientAuthenticationQueue.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the HttpRequestQueueResultCode2 type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;
using Photon.Common.Authentication.Data;
using Photon.SocketServer.Diagnostics;
using Photon.SocketServer.Net;

namespace Photon.Common.Authentication.CustomAuthentication
{
    public class AsyncHttpResponse
    {
        public AsyncHttpResponse(HttpRequestQueueResultCode status, bool rejectIfUnavailable, object state)
        {
            this.Status = status;
            this.State = state;
            this.RejectIfUnavailable = rejectIfUnavailable; 
        }

        public HttpRequestQueueResultCode Status { get; private set; }

        public object State { get; set; }

        public byte[] ResponseData { get; set; }

        public bool RejectIfUnavailable { get; set; }

        public long ElapsedTicks { get; set; }
    }

    public interface IClientAuthenticationQueue
    {
        NameValueCollection QueryStringParametersCollection { get; }
        string Uri { get; }
        string QueryStringParameters { get; }
        bool RejectIfUnavailable { get; }
        bool ForwardAsJSON { get; }

        ClientAuthenticationType ClientAuthenticationType { get; }

        object CustomData { get; }

        void EnqueueRequest(string clientQueryStringParamters, byte[] postData, string contentType,
            Action<AsyncHttpResponse, IClientAuthenticationQueue> callback, object state);

        void EnqueueRequestWithExpectedStatusCodes(HttpWebRequest webRequest, byte[] postData, Action<AsyncHttpResponse, IClientAuthenticationQueue> callback, object state, List<HttpStatusCode> expectedStatusCodes);
    }

    public class ClientAuthenticationQueue : IClientAuthenticationQueue
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly PoolFiber fiber;

        private readonly int requestTimeoutMilliseconds;

        private readonly RoundRobinCounter timeoutCounter = new RoundRobinCounter(100);

        private readonly HttpRequestQueue httpRequestQueue;

        private readonly RoundRobinCounter RequestTimeCounter = new RoundRobinCounter(100);

        private readonly LogCountGuard execRequestLogGuard = new LogCountGuard(new TimeSpan(0, 1, 0));

        public ClientAuthenticationQueue(string uri, string queryStringParameters, bool rejectIfUnavailable, int requestTimeout, bool forwardAsJSON)
        {
            this.Uri = uri;
            this.QueryStringParameters = queryStringParameters;

            if (!string.IsNullOrEmpty(queryStringParameters))
            {
                this.QueryStringParametersCollection = HttpUtility.ParseQueryString(queryStringParameters);
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Create authentication queue for adress {0}", this.Uri);
            }

            this.requestTimeoutMilliseconds = requestTimeout;
            this.RejectIfUnavailable = rejectIfUnavailable; 

            this.fiber = new PoolFiber();

            this.httpRequestQueue = new HttpRequestQueue(this.fiber);
            this.ForwardAsJSON = forwardAsJSON;
        }

        #region Properties
        public int CurrentRequests { get { return this.httpRequestQueue.RunningRequestsCount; } }

        public TimeSpan ReconnectInterval
        {
            get
            {
                return this.httpRequestQueue.ReconnectInterval;
            }
            set
            {
                this.httpRequestQueue.ReconnectInterval = value;
            }
        }

        public TimeSpan QueueTimeout
        {
            get
            {
                return this.httpRequestQueue.QueueTimeout;
            }

            set
            {
                this.httpRequestQueue.QueueTimeout = value;
            }
        }

        public int MaxQueuedRequests
        {
            get
            {
                return this.httpRequestQueue.MaxQueuedRequests;
            }
            set
            {
                this.httpRequestQueue.MaxQueuedRequests = value;
            }
        }

        public int MaxConcurrentRequests
        {
            get
            {
                return this.httpRequestQueue.MaxConcurrentRequests;
            }

            set
            {
                this.httpRequestQueue.MaxConcurrentRequests = value;
            }
        }

        public int MaxErrorRequests
        {
            get
            {
                return this.httpRequestQueue.MaxErrorRequests;
            }

            set
            {
                this.httpRequestQueue.MaxErrorRequests = value;
            }
        }

        public int MaxTimedOutRequests
        {
            get
            {
                return this.httpRequestQueue.MaxTimedOutRequests;
            }

            set
            {
                this.httpRequestQueue.MaxTimedOutRequests = value;
            }
        }

        public int MaxBackoffTimeInMiliseconds
        {
            get
            {
                return this.httpRequestQueue.MaxBackoffInMilliseconds;
            }

            set
            {
                this.httpRequestQueue.MaxBackoffInMilliseconds = value;
            }
        }

        public ClientAuthenticationType ClientAuthenticationType { get; set; }
        public object CustomData { get; set; }

        public NameValueCollection QueryStringParametersCollection { get; private set; }

        public string Uri { get; private set; }

        public string QueryStringParameters { get; private set; }

        public bool RejectIfUnavailable { get; private set; }

        public bool ForwardAsJSON { get; private set; }

        #endregion

        #region Publics

        public void SetHttpRequestQueueCounters(IHttpRequestQueueCounters counters)
        {
            this.httpRequestQueue.SetCounters(counters);
        }

        public void EnqueueRequest(string clientQueryStringParamters, byte[] postData, string contentType, Action<AsyncHttpResponse, IClientAuthenticationQueue> callback, object state)
        {
            this.fiber.Enqueue(() => this.ExecuteRequest(clientQueryStringParamters, postData, contentType, callback, state));
        }

        public void EnqueueRequestWithExpectedStatusCodes(HttpWebRequest webRequest, byte[] postData, Action<AsyncHttpResponse, IClientAuthenticationQueue> callback, object state, List<HttpStatusCode> expectedStatusCodes)
        {
            this.fiber.Enqueue(() => this.ExecuteRequestWithExpectedStatusCodes(webRequest, postData, callback, state, expectedStatusCodes));
        }

        #endregion

        #region .privates


        private void ExecuteRequest(string clientAuthenticationRequestUrl, byte[] postData, string contentType, Action<AsyncHttpResponse, ClientAuthenticationQueue> callback, object state)
        {
            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(clientAuthenticationRequestUrl);
                webRequest.Proxy = null;
                webRequest.Timeout = this.requestTimeoutMilliseconds;
                webRequest.ContentType = contentType;

                HttpRequestQueueCallback queueCallback = 
                    (result, httpRequest, userState) => 
                        this.fiber.Enqueue(() => this.OnCallback(result, httpRequest, userState, callback));


                if (postData != null)
                {
                    webRequest.Method = "POST";
                    this.httpRequestQueue.Enqueue(webRequest, postData, queueCallback, state);
                }
                else
                {
                    webRequest.Method = "GET";
                    this.httpRequestQueue.Enqueue(webRequest, queueCallback, state);
                }
            }
            catch (Exception ex)
            {
                var message = string.Format("Exception during ExecuteRequest to url '{0}'. Exception Msg:{1}", clientAuthenticationRequestUrl, ex.Message);
                log.Error(this.execRequestLogGuard, message, ex);
                ThreadPool.QueueUserWorkItem(delegate { callback(new AsyncHttpResponse(HttpRequestQueueResultCode.Error, this.RejectIfUnavailable, state), this); });
            }
        }

        //added for Viveport, the HttpWebRequest requires additional configuration
        private void ExecuteRequestWithExpectedStatusCodes(HttpWebRequest webRequest, byte[] postData, Action<AsyncHttpResponse, ClientAuthenticationQueue> callback, object state, List<HttpStatusCode> expectedStatusCodes)
        {
            try
            {
                webRequest.Proxy = null;
                webRequest.Timeout = this.requestTimeoutMilliseconds;

                HttpRequestQueueCallback queueCallback =
                    (result, httpRequest, userState) =>
                        this.fiber.Enqueue(() => this.OnCallbackReturnExpectedStatusCodes(result, httpRequest, userState, callback, expectedStatusCodes));


                if (postData != null)
                {
                    webRequest.Method = "POST";
                    this.httpRequestQueue.Enqueue(webRequest, postData, queueCallback, state);
                }
                else
                {
                    webRequest.Method = "GET";
                    this.httpRequestQueue.Enqueue(webRequest, queueCallback, state);
                }
            }
            catch (Exception ex)
            {
                var message = string.Format("Exception during ExecuteRequest to url '{0}'. Exception Msg:{1}",
                    webRequest.RequestUri, ex.Message);
                log.Error(message, ex);
                ThreadPool.QueueUserWorkItem(delegate { callback(new AsyncHttpResponse(HttpRequestQueueResultCode.Error, this.RejectIfUnavailable, state), this); });
            }
        }

        private void OnCallback(HttpRequestQueueResultCode resultCode, 
            AsyncHttpRequest result, object userState, Action<AsyncHttpResponse, ClientAuthenticationQueue> userCallback)
        {
            try
            {
                var url = result.WebRequest.RequestUri;
                byte[] responseData = null;
                var status = result.Status;
                var exception = result.Exception;

                this.RequestTimeCounter.AddValue((int)result.Elapsedtime.TotalMilliseconds);
                if (result.Response != null)
                {
                    responseData = result.Response;
                }

                result.Dispose();

                byte[] resultResponseData = null;
                switch (resultCode)
                {
                    case HttpRequestQueueResultCode.Success:
                    {
                        if (log.IsDebugEnabled)
                        {
                            var responseString = string.Empty;
                            if (responseData != null)
                            {
                                responseString = Encoding.UTF8.GetString(responseData);
                            }

                            log.DebugFormat(
                                "Custom authentication result: uri={0}, status={1}, msg={2}, data={3}",
                                url,
                                status,
                                exception != null ? exception.Message : string.Empty,
                                responseString);
                        }

                        this.timeoutCounter.AddValue(0);
                        resultResponseData = responseData;
                    }
                        break;
                    case HttpRequestQueueResultCode.RequestTimeout:
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Custom authentication timed out: uri={0}, status={1}, msg={2}",
                                url, status, exception != null ? exception.Message : string.Empty);
                        }
                        this.timeoutCounter.AddValue(1);
                    }
                        break;
                    case HttpRequestQueueResultCode.QueueFull:
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat(
                                "Custom authentication error: queue is full. Requests count {0}, url:{1}, msg:{2}",
                                this.httpRequestQueue.QueuedRequestCount, url,
                                exception != null ? exception.Message : string.Empty);
                        }
                    }
                        break;
                    case HttpRequestQueueResultCode.QueueTimeout:
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Custom authentication error: Queue timedout. uri={0}, status={1}, msg={2}",
                                url, status, exception != null ? exception.Message : string.Empty);
                        }
                        break;
                    case HttpRequestQueueResultCode.Error:
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Custom authentication error: uri={0}, status={1}, msg={2}",
                                url, status, exception != null ? exception.Message : string.Empty);
                        }
                        resultResponseData = responseData;
                    }
                        break;
                    case HttpRequestQueueResultCode.Offline:
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Custom auth error. Queue is offline. url:{0}, status{1}, msg:{2}",
                                url, status, exception != null ? exception.Message : string.Empty);
                        }
                    }
                        break;
                }

                var response = new AsyncHttpResponse(resultCode, this.RejectIfUnavailable, userState)
                {
                    ResponseData = resultResponseData,
                    ElapsedTicks = result.ElapsedTicks,
                };

                ThreadPool.QueueUserWorkItem(delegate { userCallback(response, this); });
                return;
            }
            catch (Exception e)
            {
                log.Error(e);
            }


            ThreadPool.QueueUserWorkItem(delegate { userCallback(new AsyncHttpResponse(HttpRequestQueueResultCode.Error, this.RejectIfUnavailable, userState), this); });
        }

        //added for Viveport, they use 4XX status codes when token expires
        private void OnCallbackReturnExpectedStatusCodes(HttpRequestQueueResultCode resultCode, AsyncHttpRequest result, object userState, Action<AsyncHttpResponse, ClientAuthenticationQueue> userCallback, List<HttpStatusCode> expectedStatusCodes)
        {
            try
            {
                var url = result.WebRequest.RequestUri;
                byte[] responseData = null;
                var status = result.Status;
                var exception = result.Exception;
                
                this.RequestTimeCounter.AddValue((int)result.Elapsedtime.TotalMilliseconds);
                if (result.Response != null)
                {
                    responseData = result.Response;
                }

                if (resultCode == HttpRequestQueueResultCode.Error && expectedStatusCodes != null && expectedStatusCodes.Any(expectedStatusCode => expectedStatusCode == result.WebResponse.StatusCode))
                {
                    resultCode = HttpRequestQueueResultCode.Success;
                    responseData = Encoding.UTF8.GetBytes(string.Format("{0}/{1}", (int)result.WebResponse.StatusCode, result.WebResponse.StatusDescription));
//                    responseData = Encoding.UTF8.GetBytes(((int)result.WebResponse.StatusCode).ToString());
                }

                result.Dispose();

                byte[] resultResponseData = null;
                switch (resultCode)
                {
                    case HttpRequestQueueResultCode.Success:
                        {
                            if (log.IsDebugEnabled)
                            {
                                var responseString = string.Empty;
                                if (responseData != null)
                                {
                                    responseString = Encoding.UTF8.GetString(responseData);
                                }

                                log.DebugFormat(
                                    "Custom authentication result: uri={0}, status={1}, msg={2}, data={3}",
                                    url,
                                    status,
                                    exception != null ? exception.Message : string.Empty,
                                    responseString);
                            }

                            this.timeoutCounter.AddValue(0);
                            resultResponseData = responseData;
                        }
                        break;
                    case HttpRequestQueueResultCode.RequestTimeout:
                        {
                            if (log.IsDebugEnabled)
                            {
                                log.DebugFormat("Custom authentication timed out: uri={0}, status={1}, msg={2}",
                                    url, status, exception != null ? exception.Message : string.Empty);
                            }
                            this.timeoutCounter.AddValue(1);
                        }
                        break;
                    case HttpRequestQueueResultCode.QueueFull:
                        {
                            if (log.IsDebugEnabled)
                            {
                                log.DebugFormat(
                                    "Custom authentication error: queue is full. Requests count {0}, url:{1}, msg:{2}",
                                    this.httpRequestQueue.QueuedRequestCount, url,
                                    exception != null ? exception.Message : string.Empty);
                            }
                        }
                        break;
                    case HttpRequestQueueResultCode.QueueTimeout:
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Custom authentication error: Queue timedout. uri={0}, status={1}, msg={2}",
                                url, status, exception != null ? exception.Message : string.Empty);
                        }
                        break;
                    case HttpRequestQueueResultCode.Error:
                        {
                            if (log.IsDebugEnabled)
                            {
                                log.DebugFormat("Custom authentication error: uri={0}, status={1}, msg={2}",
                                    url, status, exception != null ? exception.Message : string.Empty);
                            }
                        }
                        break;
                    case HttpRequestQueueResultCode.Offline:
                        {
                            if (log.IsDebugEnabled)
                            {
                                log.DebugFormat("Custom auth error. Queue is offline. url:{0}, status{1}, msg:{2}",
                                    url, status, exception != null ? exception.Message : string.Empty);
                            }
                        }
                        break;
                }

                var response = new AsyncHttpResponse(resultCode, this.RejectIfUnavailable, userState)
                {
                    ResponseData = resultResponseData,
                    ElapsedTicks = result.ElapsedTicks,
                };

                ThreadPool.QueueUserWorkItem(delegate { userCallback(response, this); });
                return;
            }
            catch (Exception e)
            {
                log.Error(e);
            }


            ThreadPool.QueueUserWorkItem(delegate { userCallback(new AsyncHttpResponse(HttpRequestQueueResultCode.Error, this.RejectIfUnavailable, userState), this); });
        }

        #endregion

        public class RoundRobinCounter
        {
            private readonly int[] values;
            private int sum;
            private int pos;
            private int count;

            public RoundRobinCounter(int size)
            {
                this.values = new int[size];
            }

            public int Sum
            {
                get { return this.sum; }
            }

            public int Average
            {
                get { return this.Sum / (this.count > 0 ? this.count : 1); }
            }

            public void AddValue(int v)
            {
                if (this.count < this.values.Length)
                {
                    this.count++;
                }

                this.sum -= this.values[this.pos];  
                this.sum += v;
                this.values[this.pos] = v;
                this.pos = this.pos + 1;

                if (this.pos >= this.values.Length)
                {
                    this.pos = 0;
                }
            }
        }
    }
}
