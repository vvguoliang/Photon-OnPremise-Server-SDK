using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;
using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;
using Newtonsoft.Json;
using Photon.Common.Authentication.Data;
using Photon.SocketServer.REST;
using PhotonCloud.Authentication;

namespace PhotonCloud.NameServer.Monitoring
{
    public class MonitorRequestHandler : RestRequestHandler
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public PoolFiber fiber;

        private PhotonCloudApp photonCloudApp;

        public MonitoringCache MonitoringCache { get; private set; }

        public MonitorRequestHandler(PhotonCloudApp application)
        {
            fiber = new PoolFiber();
            fiber.Start();

            photonCloudApp = application;

            MonitoringService monitoringService = new MonitoringService(Settings.Default.MonitoringApiEndpoint, 5000);
            this.MonitoringCache = MonitoringCache.CreateCache(monitoringService);
        }


        public static string Path
        {
            get { return "monitoring"; }
        }

        public bool AddHandler(/*ApplicationBase application*/)
        {
            var result = photonCloudApp.AddRestRequestHandler(Path, this);

            if (log.IsInfoEnabled)
            {
                log.InfoFormat("MonitorRequestHandler, AddHandler: {0}", result);
            }

            return result;
        }

        protected override void Get(RestRequest request, IRestRequestContext context)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("MonitorRequestHandler, get '{0}'", request.Uri);
            }

            var queryParams = HttpUtility.ParseQueryString(request.Uri.Query);

            var appId = queryParams["appid"];

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("MonitorRequestHandler, appId '{0}'", appId);
            }

            if (string.IsNullOrEmpty(appId))
            {
                context.SendResponse("AppId missing");
            }

            //TODO change this method to a check and only and call GetMonitoringResult separately?
            //TODO how to decide if the servernames have to be updated?
//            if (this.MonitoringCache.GetMonitoringResultFromCache(appId, GetMonitoringResultCallback, context))
//            {
//                return;
//            }

            photonCloudApp.AuthenticationCache.GetAccount(appId, fiber, account => this.OnGetApplicationAccount(account, request, context, queryParams));
        }

        public void OnGetApplicationAccount(ApplicationAccount applicationAccount, RestRequest request, IRestRequestContext context, NameValueCollection queryParams)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("MonitorRequestHandler, OnGetApplicationAccount '{0}': {1}/{2}", applicationAccount.ApplicationId, applicationAccount.PrivateCloud, applicationAccount.ServiceType);
            }

            if (string.IsNullOrEmpty(applicationAccount.ApplicationId))
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("MonitorRequestHandler, OnGetApplicationAccount error: '{0}'", applicationAccount.DebugMessage);
                }
                context.SendResponse("Could not retrieve account information");
                return;
            }

            if (applicationAccount.ServiceType != ServiceType.Realtime && applicationAccount.ServiceType != ServiceType.Pun)
            {
                context.SendResponse("Only Realtime applications are supported. Application has ServiceType " + applicationAccount.ServiceType);
                return;
            }

            var servers = this.photonCloudApp.CloudCache.TryGetPhotonEndpoints(applicationAccount.PrivateCloud, applicationAccount.ServiceType);
            if (servers == null || servers.Count == 0)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("No servers found for appId '{0}' cloud '{1}' ServiceType '{2}'", applicationAccount.ApplicationId, applicationAccount.PrivateCloud, applicationAccount.ServiceType);
                }
                context.SendResponse("Found no servers for App");
                return;
            }

            var servernamesList = new List<string>();

            //TODO ? add cluster filter param?
            foreach (var photonEndpointInfo in servers)
            {
                var servernameAndRegion = GetServernameAndRegion(photonEndpointInfo);
                if (string.IsNullOrEmpty(servernameAndRegion))
                {
                    continue;
                }

                servernamesList.Add(servernameAndRegion);
            }

            var servernames = string.Join(";", servernamesList.ToArray());

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Call GetMonitoringResult for application {0}/{1}: {2}", applicationAccount.ApplicationId, applicationAccount.PrivateCloud, servernames);
            }

            this.MonitoringCache.GetMonitoringResult(applicationAccount.ApplicationId, servernames, GetMonitoringResultCallback, context);
        }

        public void GetMonitoringResultCallback(MonitoringResult monitoringResult, object state)
        {
            var context = (IRestRequestContext) state;

            fiber.Enqueue(() => HandleGetMonitoringResult(monitoringResult, context));
        }

        private void HandleGetMonitoringResult(MonitoringResult monitoringResult, IRestRequestContext context)
        {
            if (monitoringResult.MonitoringServiceResult == MonitoringServiceResult.Ok)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("HandleGetMonitoringResult, response for app {0}: {1}", monitoringResult.ApplicationId, monitoringResult.Status);
                }
                context.SendResponse(monitoringResult.Status);
            }
            else
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("HandleGetMonitoringResult, MonitoringServiceResult {0}: {1}", monitoringResult.MonitoringServiceResult, monitoringResult.DebugMessage);
                }
                context.SendResponse("An error occured");
            }
        }

        private string GetJson(Dictionary<string, string> statusByRegion)
        {
            return JsonConvert.SerializeObject(statusByRegion);
        }

        private string GetServername(string hostname)
        {
            var split = hostname.Split('.');
            if (split.Length > 0)
            {
                return split[0].ToLower();
            }

            return null;
        }

        private string GetServernameAndRegion(CloudPhotonEndpointInfo photonEndpointInfo)
        {
            string result = null;

            var split = photonEndpointInfo.UdpHostname.Split('.');
            if (split.Length > 0)
            {
                result = string.Format("{0}_{1}", split[0].ToLower(), photonEndpointInfo.Region);
                //append cluster if not default
                if (!photonEndpointInfo.Cluster.Equals("default"))
                {
                    result = string.Format("{0}/{1}", result, photonEndpointInfo.Cluster);
                }
            }

            return result;
        }
    }
}
