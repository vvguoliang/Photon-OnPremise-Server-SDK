// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MasterApplication.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the MasterApplication type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using Photon.Common.Authentication;
using Photon.Common.Authentication.Diagnostic;
using Photon.Common.LoadBalancer;
using Photon.Common.Misc;
using Photon.Hive.Common;
using Photon.Hive.WebRpc;
using Photon.Hive.WebRpc.Configuration;
using Photon.LoadBalancing.ServerToServer.Operations;
using Photon.SocketServer.Diagnostics;
using Photon.SocketServer.Rpc.Protocols;

namespace Photon.LoadBalancing.MasterServer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Runtime.InteropServices;

    using ExitGames.Logging;
    using ExitGames.Logging.Log4Net;

    using log4net;
    using log4net.Config;
    using Photon.LoadBalancing.Common;
    using Photon.LoadBalancing.MasterServer.GameServer;
    using Photon.SocketServer;

    using Photon.LoadBalancing.Handler;

    using LogManager = ExitGames.Logging.LogManager;
    using Photon.LoadBalancing.Manger;
    using Photon.LoadBalancing.Model;

    public class MasterApplication : ApplicationBase
    {
        #region Constants and Fields

        public static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private NodesReader reader;

        private readonly WebRpcManager webRpcManager;
        #endregion

        #region Constructor Desctructor

        public static MasterApplication Instance
        {
            get;
            private set;
        }

        public MasterApplication()
        {
            var env = new Dictionary<string, object>
            {
                {"AppId", this.HwId},
                {"AppVersion", ""},
                {"Region", ""},
                {"Cloud", ""},
            };

            var options = new HttpRequestQueueOptions(
                CommonSettings.Default.WebRpcHttpQueueMaxErrors,
                CommonSettings.Default.WebRpcHttpQueueMaxTimeouts,
                CommonSettings.Default.WebRpcHttpQueueRequestTimeout,
                CommonSettings.Default.WebRpcHttpQueueQueueTimeout,
                CommonSettings.Default.WebRpcHttpQueueMaxBackoffTime,
                CommonSettings.Default.WebRpcHttpQueueReconnectInterval,
                CommonSettings.Default.WebRpcHttpQueueMaxQueuedRequests,
                CommonSettings.Default.WebRpcHttpQueueMaxConcurrentRequests);

            var settings = WebRpcSettings.Default;
            var webRpcEnabled = (settings != null && settings.Enabled);
            var baseUrlString = webRpcEnabled ? settings.BaseUrl.Value : string.Empty;

            this.webRpcManager = new WebRpcManager(webRpcEnabled, baseUrlString, env, options);
        }

        #endregion

        #region Properties

        public static ApplicationStats AppStats { get; protected set; }

        public GameServerContextManager GameServers { get; protected set; }

        public bool IsMaster
        {
            get
            {
                return this.MasterNodeId == this.LocalNodeId;
            }
        }

        public LoadBalancer<GameServerContext> LoadBalancer { get; protected set; }

        protected byte LocalNodeId
        {
            get
            {
                return this.reader.CurrentNodeId;
            }
        }

        protected byte MasterNodeId { get; set; }

        public GameApplication DefaultApplication { get; protected set; }

        public AuthTokenFactory TokenCreator { get; protected set; }

        public CustomAuthHandler CustomAuthHandler { get; set; }

        private S2SCustomTypeCacheMan S2SCustomTypeCacheMan { get; set; }

        #endregion

        #region Public Methods

        public IPAddress GetInternalMasterNodeIpAddress()
        {
            return this.reader.GetIpAddress(this.MasterNodeId);
        }

        public CustomTypeCache GetS2SCustomTypeCache()
        {
            return this.S2SCustomTypeCacheMan.GetCustomTypeCache();
        }

        public virtual void OnServerWentOffline(GameServerContext gameServerContext)
        {
            this.RemoveGameServerFromLobby(gameServerContext);

            if (AppStats != null)
            {
                AppStats.HandleGameServerRemoved(gameServerContext);
            }
        }

        #endregion

        #region Methods

        protected override PeerBase CreatePeer(InitRequest initRequest)
        {

            if (this.IsGameServerPeer(initRequest))
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Received init request from game server");
                }
                return this.CreateGameServerPeer(initRequest);
            }
            log.Info("一个客户端连接中......MasterAppliction11");
            if (this.LocalNodeId == this.MasterNodeId)
            {
                log.Info("一个客户端连接中......MasterAppliction111");
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Received init request from game client on leader node");
                }

                var peer = new MasterClientPeer(initRequest);
                peerList.Add(peer);
                for (var i = 0; peerList.Count > i; i++)
                {
                    log.Info("====展示信息===:" + peerList[i].ToString());
                }
                log.Info("====客户端:========:" + peerList.ToString());
                if (this.webRpcManager.IsRpcEnabled)
                {
                    peer.WebRpcHandler = this.webRpcManager.GetWebRpcHandler();
                }

                return peer;
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Received init request from game client on slave node");
            }
            log.Info("一个客户端连接中......MasterAppliction11111");
            return new RedirectedClientPeer(initRequest);
        }

        public List<ClientPeer> peerList = new List<ClientPeer>();

        protected PeerBase CreateGameServerPeer(InitRequest initRequest)
        {
            var peer = this.CreateMasterServerPeer(initRequest);

            if (initRequest.InitObject == null)
            {
                return peer;
            }

            var request = new RegisterGameServerDataContract(initRequest.Protocol, (Dictionary<byte, object>)initRequest.InitObject);
            if (!request.IsValid)
            {
                log.WarnFormat("Can not register server. Init request from {0}:{1} is invalid:{2}", initRequest.RemoteIP, initRequest.RemotePort, request.GetErrorMessage());
                return null;
            }

            this.GameServers.RegisterGameServerOnInit(request, peer);

            if (!peer.IsRegistered)
            {
                return null;
            }

            initRequest.ResponseObject = peer.GetRegisterResponse();
            return peer;
        }

        protected virtual IncomingGameServerPeer CreateMasterServerPeer(InitRequest initRequest)
        {
            return new IncomingGameServerPeer(initRequest, this);
        }

        protected virtual void Initialize()
        {
            if (CommonSettings.Default.EnablePerformanceCounters)
            {
                this.InitCorePerformanceCounters();
                CustomAuthResultCounters.Initialize();
            }
            else
            {
                log.Info("Performance counters are disabled");
            }

            this.GameServers = new GameServerContextManager(this, MasterServerSettings.Default.GSContextTTL);
            this.LoadBalancer = new LoadBalancer<GameServerContext>(Path.Combine(this.ApplicationRootPath, "LoadBalancer.config"));

            this.DefaultApplication = new GameApplication("{Default}", "{Default}", this.LoadBalancer);

            this.CustomAuthHandler = new CustomAuthHandler(new HttpRequestQueueCountersFactory());
            this.CustomAuthHandler.InitializeFromConfig();

            if (MasterServerSettings.Default.AppStatsPublishInterval > 0)
            {
                AppStats = new ApplicationStats(MasterServerSettings.Default.AppStatsPublishInterval);
            }

            //            CounterPublisher.DefaultInstance.AddStaticCounterClass(typeof(LoadBalancerCounter), this.ApplicationName);

            this.InitResolver();
        }

        protected virtual bool IsGameServerPeer(InitRequest initRequest)
        {
            return initRequest.LocalPort == MasterServerSettings.Default.IncomingGameServerPeerPort;
        }

        protected override void OnStopRequested()
        {
            // in case of application restarts, we need to disconnect all GS peers to force them to reconnect. 
            if (log.IsInfoEnabled)
            {
                log.InfoFormat("OnStopRequested... going to disconnect {0} GS peers", this.GameServers != null ? this.GameServers.Count : 0);
            }

            // copy to prevent changes of the underlying enumeration
            if (this.GameServers != null)
            {
                var gameServers = this.GameServers.GameServerPeersToArray();

                foreach (IncomingGameServerPeer peer in gameServers)
                {
                    if (peer != null)
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Disconnecting GS peer {0}:{1}", peer.RemoteIP, peer.RemotePort);
                        }

                        peer.Disconnect();
                    }
                }
            }
        }

        private void SetupTokenCreator()
        {
            var sharedKey = Photon.Common.Authentication.Settings.Default.AuthTokenKey;
            if (string.IsNullOrEmpty(sharedKey))
            {
                log.WarnFormat("AuthTokenKey not specified in config. Authentication tokens are not supported");
                return;
            }

            var hmacKey = Photon.Common.Authentication.Settings.Default.HMACTokenKey;
            if (string.IsNullOrEmpty(hmacKey))
            {
                log.Warn("HMACTokenKey not specified in config");
            }

            int expirationTimeSeconds = Photon.Common.Authentication.Settings.Default.AuthTokenExpirationS;
            //if (expirationTimeSeconds <= 0)
            //{
            //    log.ErrorFormat("Authentication token expiration to low: expiration={0} seconds", expirationTimeSeconds);
            //}

            var expiration = TimeSpan.FromSeconds(expirationTimeSeconds);
            this.TokenCreator = GetAuthTokenFactory();
            this.TokenCreator.Initialize(sharedKey, hmacKey, expiration, "MS:" + Environment.MachineName);

            log.InfoFormat("TokenCreator intialized with an expiration of {0}", expiration);
        }

        protected virtual AuthTokenFactory GetAuthTokenFactory()
        {
            return new AuthTokenFactory();
        }

        protected override void Setup()
        {
            InitLogging();

            this.S2SCustomTypeCacheMan = new S2SCustomTypeCacheMan();

            log.InfoFormat("Master server initialization started");

            Protocol.AllowRawCustomValues = true;
            Protocol.RegisterTypeMapper(new UnknownTypeMapper());
            this.SetUnmanagedDllDirectory();

            this.SetupTokenCreator();

            this.Initialize();

            log.InfoFormat("Master server initialization finished-11111111111111111111111111111111");
            IUserManager userManager = new UserManager();
            //log.Info(userManager.VerifyUser("11", "11"));
            //log.Info(userManager.VerifyUser("wer1", "wer1"));
            User user = userManager.GetById(7);
            log.Info(user.Username);
            log.Info(user.Password);
        }

        protected virtual void InitLogging()
        {
            log4net.GlobalContext.Properties["Photon:ApplicationLogPath"] = Path.Combine(Path.Combine(this.ApplicationRootPath, "bin_win64"), "log");
            log4net.GlobalContext.Properties["LogFileName"] = "GS" + this.ApplicationName;
            FileInfo configFileInfo = new FileInfo(Path.Combine(this.BinaryPath, "log4net.config"));

            if (configFileInfo.Exists)
            {
                LogManager.SetLoggerFactory(Log4NetLoggerFactory.Instance);
                XmlConfigurator.ConfigureAndWatch(configFileInfo);//插件读取插件
            }

            log.Info("开启运行.......!111111111111111111111111");

            InHandler();
        }

        public Dictionary<OperationCode, BaseHandler> Handlerict = new Dictionary<OperationCode, BaseHandler>();

        public void InHandler()
        {
            LoginHandler loginHandler = new LoginHandler();
            Handlerict.Add(loginHandler.OpCode, loginHandler);
            DefaultHandler defaultHandler = new DefaultHandler();
            Handlerict.Add(defaultHandler.OpCode, defaultHandler);
            RegisterHandler registerHandler = new RegisterHandler();
            Handlerict.Add(registerHandler.OpCode, registerHandler);
            SyncPlayerHandler syncPlayerHandler = new SyncPlayerHandler();
            Handlerict.Add(syncPlayerHandler.OpCode, syncPlayerHandler);
        }

        protected override void TearDown()
        {
            log.InfoFormat("Master server TearDown is called. Master server stopped 11111111111111111111111111111111111111111111111");
            log.Info("服务器应用关闭");
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectory(string lpPathName);

        /// <summary>
        /// Adds a directory to the search path used to locate the 32-bit or 64-bit version 
        /// for unmanaged DLLs used in the application.
        /// </summary>
        /// <remarks>
        /// Assemblies having references to unmanaged libraries (like SqlLite) require either a
        /// 32-Bit or a 64-Bit version of the library depending on the current process.
        /// </remarks>
        private void SetUnmanagedDllDirectory()
        {
            string unmanagedDllDirectory = Path.Combine(this.BinaryPath, IntPtr.Size == 8 ? "x64" : "x86");
            bool result = SetDllDirectory(unmanagedDllDirectory);

            if (result == false)
            {
                log.WarnFormat("Failed to set unmanaged dll directory to path {0}", unmanagedDllDirectory);
            }
        }

        private void InitResolver()
        {
            string nodesFileName = CommonSettings.Default.NodesFileName;
            if (string.IsNullOrEmpty(nodesFileName))
            {
                nodesFileName = "Nodes.txt";
            }

            this.reader = new NodesReader(this.ApplicationRootPath, nodesFileName);

            // TODO: remove Proxy code completly
            //if (this.IsResolver && MasterServerSettings.Default.EnableProxyConnections)
            //{
            //    // setup for proxy connections
            //    this.reader.NodeAdded += this.NodesReader_OnNodeAdded;
            //    this.reader.NodeChanged += this.NodesReader_OnNodeChanged;
            //    this.reader.NodeRemoved += this.NodesReader_OnNodeRemoved;
            //    log.Info("Proxy connections enabled");
            //}

            this.reader.Start();

            // use local host id if nodes.txt does not exist or if line ending with 'Y' does not exist, otherwise use fixed node #1
            this.MasterNodeId = (byte)(this.LocalNodeId == 0 ? 0 : 1);

            log.InfoFormat(
             "Current Node (nodeId={0}) is {1}the active master (leader)",
             this.reader.CurrentNodeId,
             this.MasterNodeId == this.reader.CurrentNodeId ? string.Empty : "NOT ");
        }

        protected virtual void RemoveGameServerFromLobby(GameServerContext gameServerContext)
        {
            this.DefaultApplication.OnGameServerRemoved(gameServerContext);
        }

        #endregion
    }
}