namespace CustomAuthService
{
    using System;
    using System.IO;
    using System.Threading;

    using CustomAuthService.Properties;

    using ExitGames.Logging;
    using ExitGames.Logging.Log4Net;

    using log4net;
    using log4net.Config;

    using Microsoft.Owin.Hosting;

    using Newtonsoft.Json;

    using Photon.SocketServer;

    using LogManager = ExitGames.Logging.LogManager;

    public class Application : ApplicationBase
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private IDisposable webApp;

        private RuntimeConfig runtimeConfig;

        private FileSystemWatcher runitmeConfigWatcher;

        protected override PeerBase CreatePeer(InitRequest initRequest)
        {
            log.Error("Service does not allow incomming connection through peers");
            return null;
        }

        protected override void Setup()
        {
            this.SetupLogging();

            log.Info("Initing custom auth applicaiton");

            this.StartHttpService();

            this.LoadRuntimeConfig();
            this.SetupRuntimeConfigWatcher();
        }

        private void SetupRuntimeConfigWatcher()
        {
            this.runitmeConfigWatcher = new FileSystemWatcher(Instance.BinaryPath)
                                            {
                                                NotifyFilter = NotifyFilters.LastWrite,
                                                Filter = "config.json",
                                                EnableRaisingEvents = true
                                            };

            this.runitmeConfigWatcher.Changed += (source, e) => this.LoadRuntimeConfig();
        }

        private void LoadRuntimeConfig()
        {
            try
            {
                Thread.Sleep(100);
                var path = Path.Combine(ApplicationBase.Instance.BinaryPath, "config.json");
                using (var reader = File.OpenText(path))
                {
                    var result = reader.ReadToEnd();
                    var newConfig = JsonConvert.DeserializeObject<RuntimeConfig>(result);
                    if (newConfig == null)
                    {
                        log.Info("Failed to reoload config");
                        return;
                    }
                    this.runtimeConfig = newConfig;
                    log.InfoFormat("Config was updated");
                }
            }
            catch (Exception e)
            {
                log.ErrorFormat("Exception during config parsing: {0}", e);
            }
        }

        private void StartHttpService()
        {
            webApp = WebApp.Start<Startup>(url: Settings.Default.BaseAddress);
            
        }

        private void SetupLogging()
        {
            LogManager.SetLoggerFactory(Log4NetLoggerFactory.Instance);
            GlobalContext.Properties["Photon:ApplicationLogPath"] = Path.Combine(this.ApplicationRootPath, "log");
            GlobalContext.Properties["LogFileName"] = "CA" + this.ApplicationName;
            XmlConfigurator.ConfigureAndWatch(new FileInfo(Path.Combine(this.BinaryPath, "log4net.config")));
        }

        protected override void TearDown()
        {
            if (this.runitmeConfigWatcher != null)
            {
                this.runitmeConfigWatcher.Dispose();
                this.runitmeConfigWatcher = null;
            }

            if (webApp != null)
            {
                webApp.Dispose();
                webApp = null;
            }
        }

        public RuntimeConfig GetConfig()
        {
            return this.runtimeConfig;
        }

    }
}
