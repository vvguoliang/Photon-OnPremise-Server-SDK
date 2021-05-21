namespace LoadTest
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;

    using ExitGames.Concurrency.Fibers;
    using ExitGames.Logging;
    using ExitGames.Logging.Log4Net;

    using LoadTest.Diagnostics;
    using LoadTest.Properties;

    using log4net;
    using log4net.Config;

    using Photon.SocketServer;

    using LogManager = ExitGames.Logging.LogManager;

    public class Application : ApplicationBase
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly List<ClientManager> managers = new List<ClientManager>(Environment.ProcessorCount); 

        private readonly PoolFiber fiber = new PoolFiber();

        private IDisposable printScheduller;

        private volatile int RunningManagers;

        protected override PeerBase CreatePeer(InitRequest initRequest)
        {
            log.Error("Service does not support incomming connections");
            return null;
        }

        protected override void Setup()
        {
            this.SetupLogging();

            var threadsCount = Settings.Default.ThreadsCount <= 0 ? Environment.ProcessorCount : Settings.Default.ThreadsCount;

            var perManCount = Settings.Default.ConcurrentUsers / threadsCount;
            var beginIndex = 0;
            var startupTime = Settings.Default.StartupTimeS * 1000;

            if (Settings.Default.StartPerSecond == 0)
            {
                log.InfoFormat("We are starting {0} clients in {1} threads for {2} seconds",
                    Settings.Default.ConcurrentUsers, threadsCount, Settings.Default.StartupTimeS);
                for (var i = 0; i < threadsCount - 1; ++i)
                {
                    this.managers.Add(new ClientManager(this, beginIndex, beginIndex + perManCount, startupTime));
                    beginIndex += perManCount;
                }
                this.managers.Add(new ClientManager(this, beginIndex, Settings.Default.ConcurrentUsers, startupTime));

                var runDelay = startupTime / Settings.Default.ConcurrentUsers / threadsCount;

                foreach (var manager in this.managers)
                {
                    manager.Run();
                    Thread.Sleep(runDelay);
                }
            }
            else
            {
                log.InfoFormat("We are starting {0} clients per second in {1} threads", Settings.Default.StartPerSecond, threadsCount);

                var perSecond = Settings.Default.StartPerSecond / (float)threadsCount;
                var maxClientsCount = Settings.Default.MaxClientsCount / threadsCount;

                for (var i = 0; i < threadsCount; ++i)
                {
                    this.managers.Add(new ClientManager(this, perSecond, maxClientsCount));
                }

                var runDelay = 1000.0 / perSecond / threadsCount;

                foreach (var manager in this.managers)
                {
                    manager.Run();
                    Thread.Sleep((int)runDelay);
                }
            }

            var interval = Settings.Default.LogPrintIntervalMS;
            this.fiber.Start();
            printScheduller = this.fiber.ScheduleOnInterval(CounterLogger.PrintCounter, interval, interval);
        }

        protected override void TearDown()
        {
            log.Info("LoadTest Application is tearing down");

            var d = this.printScheduller;
            if (d != null)
            {
                d.Dispose();
                this.printScheduller = null;
            }

            foreach (var manager in this.managers)
            {
                manager.Stop();
            }
        }

        private void SetupLogging()
        {
            LogManager.SetLoggerFactory(Log4NetLoggerFactory.Instance);
            GlobalContext.Properties["Photon:ApplicationLogPath"] = Path.Combine(this.ApplicationRootPath, "log");
            GlobalContext.Properties["LogFileName"] = "LT" + this.ApplicationName;
            XmlConfigurator.ConfigureAndWatch(new FileInfo(Path.Combine(this.BinaryPath, "log4net.config")));
        }


        public void IncRunningManagers()
        {
            ++this.RunningManagers;
        }

        public void DecRunningManagers()
        {
            --this.RunningManagers;
            if (this.RunningManagers == 0)
            {
                var d = this.printScheduller;
                if (d != null)
                {
                    d.Dispose();
                    this.printScheduller = null;
                }
            }
        }
    }
}
