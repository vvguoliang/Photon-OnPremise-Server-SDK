using System;
using System.Reflection;
using System.Threading;
using ExitGames.Logging;
using NUnit.Framework;
using Photon.Common.Authentication.Configuration.Auth;
using Photon.LoadBalancing.UnifiedClient;
using Photon.LoadBalancing.UnifiedClient.AuthenticationSchemes;
using Photon.LoadBalancing.UnitTests.UnifiedServer.OfflineExtra;
using Photon.LoadBalancing.UnitTests.UnifiedServer.OfflineExtra.Master;
using Photon.NameServer;
using Photon.SocketServer;
using Photon.SocketServer.UnitTesting;
using Photon.UnitTest.Utils.Basic;
using Photon.UnitTest.Utils.Basic.NUnitClients;

namespace Photon.LoadBalancing.UnitTests.UnifiedServer.Policy
{

    public class OfflineConnectPolicy : LBConnectPolicyBase
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private PhotonApplicationProxy<PhotonApp> nameServer;
        private PhotonApplicationProxy<TestMasterApplication> masterServer;
        private PhotonApplicationProxy<TestApplication> gameServer;
        private PhotonApplicationProxy<TestApplication> gameServer2;
        protected PhotonApplicationHoster photonHost;

        protected string configFileName = "Photon.LoadBalancing.UnitTests.dll.config";

        protected const string NameServerAppName = "NameServer";

        #endregion

        #region Constructors

        public OfflineConnectPolicy()
        {
        }

        public OfflineConnectPolicy(IAuthenticationScheme scheme, string configFileName = "")
        {
            this.AuthenticatonScheme = scheme;

            if (!String.IsNullOrEmpty(configFileName))
            {
                this.configFileName = configFileName;
            }
        }

        #endregion

        #region Properties

        public virtual PhotonApplicationProxy MasterServer
        {
            get { return this.masterServer; }
        }

        public virtual PhotonApplicationProxy GameServer
        {
            get { return this.gameServer; }
        }

        public virtual PhotonApplicationProxy GameServer2
        {
            get { return this.gameServer2; }
        }

        public virtual PhotonApplicationProxy NameServer
        {
            get { return this.nameServer; }
        }
        
        public TestApplication GSApplication
        {
            get { return this.gameServer.Application; }
        }
   

        public override bool IsOffline
        {
            get { return true; }
        }

        public override bool IsInited
        {
            get { return (this.photonHost != null); }
        }

        #endregion

        #region Publics

        public override UnifiedClientBase CreateTestClient()
        {
            return new UnifiedTestClient(new OfflineNUnitClient(WaitTime, this), this.AuthenticatonScheme);
        }

        public override bool Setup()
        {
            log.InfoFormat("Policy Setup");

            this.PreStartChecks();
            this.photonHost = new PhotonApplicationHoster();

            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;

            this.InitApplications();

            return true;
        }

        public override void ConnectToServer(INUnitClient client, string address, object custom)
        {
            ((OfflineNUnitClient) client).Connect(this.GetProxyByAddress(address), custom);
        }

        public override void TearDown()
        {
            log.InfoFormat("Policy TearDown");
            Thread.Sleep(1500);

            this.CloseApplications();

            if (this.photonHost != null)
            {
                this.photonHost.Dispose();
                this.photonHost = null;
            }
        }

        #endregion

        #region Methods

        protected virtual void PreStartChecks()
        {
            if (this.AuthenticatonScheme.GetType() == typeof (TokenLessAuthenticationScheme))
            {
                if (AuthSettings.Default.Enabled)
                {
                    Assert.Ignore("Autentication enabled (AuthSettings Enabled=true) in Photon.LoadBalacing.config. Disable to run this tests");
                }
            }
        }

        protected virtual void InitApplications()
        {
            var codebase = Assembly.GetExecutingAssembly().CodeBase;
            var approotPath = codebase.Substring(0, codebase.IndexOf("LoadBalancing.UnitTests/bin/Debug/"));
            if (approotPath.StartsWith("file:///"))
            {
                approotPath = approotPath.Substring("file:///".Length);
            }

            this.nameServer = this.photonHost.AddApplication<PhotonApp>(
                NameServerAppName, "127.0.0.1", 4533, "Photon.NameServer.dll.config", approotPath + "../NameServer/");

            this.masterServer = this.photonHost.AddApplication<TestMasterApplication>(
                MasterServerAppName, "127.0.0.1", 4530, this.configFileName, approotPath);
            this.photonHost.AddListenerToApplication(this.MasterServer, "127.0.0.1", 4520);

            this.gameServer = this.photonHost.AddApplication<TestApplication>(
                GameServerAppName, "127.0.0.1", 4531, this.configFileName, approotPath);

            this.gameServer2 = this.photonHost.AddApplication<TestApplication>(
                GameServer2AppName, "127.0.0.1", 4532, this.configFileName, approotPath);
            ((TestApplication)this.GameServer2.Application).SetGamingTcpPort(4532);

            this.nameServer.Start();
            this.masterServer.Start();
            this.gameServer.Start();
            this.gameServer2.Start();

            // give the applications some time to connect to other servers
            Thread.Sleep(100);
        }

        protected static void StopServer<T>(ref PhotonApplicationProxy<T> server) where T : ApplicationBase
        {
            if (server != null)
            {
                server.Stop();
                server = null;
            }
        }

        protected virtual void CloseApplications()
        {
            StopServer(ref this.gameServer);
            StopServer(ref this.gameServer2);
            StopServer(ref this.masterServer);
        }

        private PhotonApplicationProxy GetProxyByAddress(string address)
        {
            if (this.photonHost == null)
            {
                return null;
            }

            var parts = address.Split(':');

            int port = Int32.Parse(parts[1]);

            PhotonApplicationProxy proxy;

            this.photonHost.TryGetApplicationProxy(parts[0], port, out proxy);
            Assert.IsNotNull(proxy, "Proxy for address '{0}:{1}' is not found", parts[0], port);
            return proxy;
        }

        #endregion
    }
}