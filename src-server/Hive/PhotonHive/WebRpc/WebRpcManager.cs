using System;
using System.Collections.Generic;
using ExitGames.Logging;
using Photon.Hive.Common;
using Photon.Hive.WebRpc.Configuration;
using Photon.SocketServer.Net;

namespace Photon.Hive.WebRpc
{
    public class WebRpcManager
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private Dictionary<string, object> environment = new Dictionary<string, object>();

        /// <summary>
        /// queue which manages requests
        /// </summary>
        private readonly HttpRequestQueue httpRequestQueue = new HttpRequestQueue();
        /// <summary>
        /// timeout for every individual request
        /// </summary>
        private int httpQueueRequestTimeout;
        /// <summary>
        /// base url for webrpc
        /// </summary>
        private string baseUrl;

        public WebRpcManager(Dictionary<string, object> environment)
        {
            var settings = WebRpcSettings.Default;
            var webRpcEnabled = (settings != null && settings.Enabled);
            var baseUrlString = webRpcEnabled ? settings.BaseUrl.Value : string.Empty;

            var options = new HttpRequestQueueOptions(httpQueueReconnectInterval: 60000);
            this.Init(webRpcEnabled, baseUrlString, environment, options);
        }

        public WebRpcManager(bool enabled, string baseUrl, Dictionary<string, object> environment, HttpRequestQueueOptions httpRequestQueueOptions)
        {
            this.Init(enabled, baseUrl, environment, httpRequestQueueOptions);
        }

        public bool IsRpcEnabled { get; private set; }

        public WebRpcHandler GetWebRpcHandler()
        {
            if (this.IsRpcEnabled)
            {
                return new WebRpcHandler(this.baseUrl, this.environment, this.httpRequestQueue, this.httpQueueRequestTimeout);
            }
            return null;
        }

        #region Methods

        private void Init(bool enabled, string baseUrlString, Dictionary<string, object> env, HttpRequestQueueOptions httpRequestQueueOptions)
        {
            this.environment = env;
            this.baseUrl = baseUrlString;

            this.httpRequestQueue.MaxErrorRequests = httpRequestQueueOptions.HttpQueueMaxTimeouts;
            this.httpRequestQueue.MaxTimedOutRequests = httpRequestQueueOptions.HttpQueueMaxErrors;
            this.httpRequestQueue.ReconnectInterval = TimeSpan.FromMilliseconds(httpRequestQueueOptions.HttpQueueReconnectInterval);
            this.httpRequestQueue.QueueTimeout = TimeSpan.FromMilliseconds(httpRequestQueueOptions.HttpQueueQueueTimeout);
            this.httpRequestQueue.MaxQueuedRequests = httpRequestQueueOptions.HttpQueueMaxQueuedRequests;
            this.httpRequestQueue.MaxBackoffInMilliseconds = httpRequestQueueOptions.HttpQueueMaxBackoffTime;
            this.httpRequestQueue.MaxConcurrentRequests = httpRequestQueueOptions.HttpQueueMaxConcurrentRequests;

            this.httpQueueRequestTimeout = httpRequestQueueOptions.HttpQueueRequestTimeout;

            this.IsRpcEnabled = enabled;
        }

        #endregion
    }
}
