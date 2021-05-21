
using System.Reflection;
using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;
using ExitGames.Threading;
using Photon.Cloud.Common.Diagnostic.HealthCheck;
using Photon.Cloud.Common.REST.HealthCheck;
using Photon.Common.Authentication;
using PhotonCloud.Authentication.AccountService.Diagnostic;
using PhotonCloud.Authentication.CustomAuth.Diagnostic;
using PhotonCloud.NameServer.Monitoring;
using PhotonCloud.NameServer.VirtualApps;

namespace PhotonCloud.NameServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Net;

    using Photon.SocketServer;
    using Photon.SocketServer.Diagnostics;

    using PhotonCloud.Authentication;
    using PhotonCloud.Authentication.Caching;

    using log4net;

    using PhotonCloud.Authentication.AccountService;

    using LogManager = ExitGames.Logging.LogManager;
    using System.Xml.Serialization;
    using Photon.NameServer;
    using Photon.NameServer.Configuration;
    using Photon.Cloud.Common.Diagnostic;
    using PhotonCloud.NameServer.Configuration;

    public class PhotonCloudApp : PhotonApp
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public readonly CustomAuthenticationCache CustomAuthenticationCache = new CustomAuthenticationCache();

        public readonly NSVirtualAppCache VirtualAppCache = new NSVirtualAppCache();

        public CloudMasterServerCache CloudCache { get; private set; }

        public IAccountService AuthenticationHandler { get; private set; }

        public AccountCache AuthenticationCache { get; private set; }

        public IHealthMonitor HealthMonitor { get; private set; }

        // only dump config information into this file for debugging (read by consul, not used by VirtualApps); 
        private const string serverConfigFile = "ServerConfig.xml";

        public PhotonCloudApp()
        {
            // create empty list to get it working correctly in case of configuration exception
            this.CloudCache = new CloudMasterServerCache(new List<Configuration.Node>());
        }

        protected override IFiber CreatePeerFiber(InitRequest request)
        {
            var executor = new BeforeAfterExecutor
            {
                BeforeExecute = () =>
                {
                    LogTagsSetup.AddRequestTags(request);
                },
                AfterExecute = () => log4net.ThreadContext.Properties.Clear()
            };

            request.UserData = executor;
            return new PoolFiber(executor);
        }

        protected override PeerBase CreatePeer(InitRequest initRequest)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Received init request: conId={0}, endPoint={1}:{2}", initRequest.ConnectionId, initRequest.LocalIP, initRequest.LocalPort);
            }
            
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Create ClientPeer");
            }

            return new ClientPeer(this, initRequest);
        }

        protected override void Setup()
        {
            base.Setup();

            this.WriteServerConfigData();
        }
        protected override void SetupLog4net()
        {
            base.SetupLog4net();            
            // Stackify: 
            GlobalContext.Properties["AppName"] = "Nameserver";
            GlobalContext.Properties["CloudType"] = Settings.Default.CloudType;
            GlobalContext.Properties["Cloud"] = Settings.Default.PrivateCloud;
            GlobalContext.Properties["Region"] = Settings.Default.Region;
            GlobalContext.Properties["Cluster"] = Settings.Default.Cluster;
            GlobalContext.Properties["CoreVersion"] = this.CoreVersion.ToString();
            GlobalContext.Properties["SdkVersion"] = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        protected override void Initialize()
        {
            base.Initialize();

            this.UseEncryptionQueue = Settings.Default.UseEncryptionQueue;
            this.EncrptionQueueLimit = Settings.Default.EncryptionQueueLimit;

            WebRequest.DefaultWebProxy = null;

            this.InitHealthMonitoring();

            this.AuthenticationHandler = AccountServiceFactory.GetAuthenticationHandler(this.HealthMonitor);
            this.AuthenticationCache = AccountCache.CreateCache(this.AuthenticationHandler, false);

            CounterPublisher.DefaultInstance.AddStaticCounterClass(typeof(Counter), this.ApplicationName);
            CounterPublisher.DefaultInstance.AddStaticCounterClass(typeof(PhotonCustomAuthCounters), this.ApplicationName);
            try
            {
                Counter.InitializePerformanceCounter(this.ApplicationName);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Failed to initialize authentication performance counter. Exception Msg:{0}", ex.Message), ex);
            }

            var monitorRequestHandler = new MonitorRequestHandler(this);
            if (!monitorRequestHandler.AddHandler(/*this*/))
            {
                log.WarnFormat("Failed to register requests handler with path:{0}", MonitorRequestHandler.Path);
            }
        }

        private void InitHealthMonitoring()
        {
            var healthMonitor = new HealthMonitor();
            this.HealthMonitor = healthMonitor;
            var healthRequestsHandler = new HealthRequestsHandler(healthMonitor);
            if (!healthRequestsHandler.AddHandler(this))
            {
                log.WarnFormat("Failed to register health requests handler with path:{0}", HealthRequestsHandler.Path);
            }
        }

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1408:ConditionalExpressionsMustDeclarePrecedence", Justification = "Reviewed. Suppression is OK here.")]
        protected  override bool ReadNameServerConfigurationFile(out string message)
        {
            var filename = Path.Combine(this.ApplicationRootPath, GetNameServerConfig());

            List<Configuration.Node> config;
            if (!Configuration.ConfigurationLoader.TryLoadFromFile(filename, out config, out message))
            {
                message = string.Format("Could not initialize Name Server list from configuration: Invalid configuration file {0}. Error: {1}", filename, message);
                return false;
            }
            
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Successfully loaded {0}.", filename);
            }
            
            this.CloudCache = new CloudMasterServerCache(config);

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Successfully updated CloudCache.");
            }

            return true; 
        }

        protected override AuthTokenFactory GetAuthTokenFactory()
        {
            return new VAppsAuthTokenFactory();
        }

        /// <inheritdoc />
        /// <summary>
        /// Write current config data to ServerConfig.xml
        /// </summary>
        protected override void WriteServerConfigData()
        {
            try
            {

                var serverConfig = new CloudServerConfig
                {
                    Cloud = Settings.Default.PrivateCloud,
                    CloudType = Settings.Default.CloudType,
                    Cluster = Settings.Default.Cluster,
                    //ServerState = (int)this.WorkloadController.ServerState,
                    Region = Settings.Default.Region,
                    ServerType = "Nameserver",
                };

                var filePath = Path.Combine(this.ApplicationRootPath, serverConfigFile);
                var serializer = new XmlSerializer(typeof(CloudServerConfig));
                using (var fs = new FileStream(filePath, FileMode.OpenOrCreate))
                {
                    serializer.Serialize(fs, serverConfig);
                }
            }
            catch (Exception e)
            {
                log.Error(e);
            }
        }
    }
}
