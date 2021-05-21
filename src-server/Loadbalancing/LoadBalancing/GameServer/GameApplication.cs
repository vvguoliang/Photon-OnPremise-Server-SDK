// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GameApplication.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the GameApplication type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Photon.Common.Authentication;
using Photon.Common.LoadBalancer;
using Photon.Common.LoadBalancer.Common;
using Photon.Common.LoadBalancer.LoadShedding;
using Photon.Common.LoadBalancer.LoadShedding.Diagnostics;
using Photon.Common.Misc;
using Photon.Hive.Common;
using Photon.Hive.Plugin;
using Photon.Hive.WebRpc;
using Photon.Hive.WebRpc.Configuration;
using Photon.LoadBalancing.ServerToServer.Operations;
using Photon.SocketServer.Rpc.Protocols;
using Photon.LoadBalancing.Handler;
using Photon.LoadBalancing.Manger;

namespace Photon.LoadBalancing.GameServer
{
    #region using directives

    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using ExitGames.Concurrency.Fibers;
    using ExitGames.Logging;
    using ExitGames.Logging.Log4Net;
    using Photon.Hive;
    using Photon.Hive.Messages;
    using log4net;
    using log4net.Config;
    using Photon.LoadBalancing.Common;
    using Photon.SocketServer;
    using Photon.SocketServer.Diagnostics;

    using ConfigurationException = ExitGames.Configuration.ConfigurationException;
    using LogManager = ExitGames.Logging.LogManager;

    #endregion

    public class GameApplication : ApplicationBase
    {
        #region Constants and Fields

        public static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly NodesReader reader;

        private ServerStateManager serverStateManager;

        private PoolFiber executionFiber;

        private readonly WebRpcManager webRpcManager;
        #endregion

        #region Constructors and Destructors

        public GameApplication()
        {
            AppDomain.CurrentDomain.AssemblyResolve += PluginManager.OnAssemblyResolve;

            this.UpdateMasterEndPoint();

            this.ServerId = Guid.NewGuid();
            this.GamingTcpPort = GameServerSettings.Default.GamingTcpPort;
            this.GamingUdpPort = GameServerSettings.Default.GamingUdpPort;
            this.GamingWebSocketPort = GameServerSettings.Default.GamingWebSocketPort;
            this.GamingSecureWebSocketPort = GameServerSettings.Default.GamingSecureWebSocketPort;
            this.GamingHttpPort = GameServerSettings.Default.GamingHttpPort;
            this.GamingHttpsPort = GameServerSettings.Default.GamingHttpsPort;
            this.GamingHttpPath = string.IsNullOrEmpty(GameServerSettings.Default.GamingHttpPath) ? string.Empty : "/" + GameServerSettings.Default.GamingHttpPath;
            this.GamingWebRTCPort = GameServerSettings.Default.GamingWebRTCPort;

            this.ConnectRetryIntervalSeconds = GameServerSettings.Default.ConnectReytryInterval;

            this.reader = new NodesReader(this.ApplicationRootPath, CommonSettings.Default.NodesFileName);


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

        #region Public Properties

        public Guid ServerId { get; private set; }

        public int? GamingTcpPort { get; protected set; }

        public int? GamingUdpPort { get; protected set; }

        public int? GamingWebSocketPort { get; protected set; }

        public int? GamingSecureWebSocketPort { get; set; }

        public int? GamingHttpPort { get; protected set; }

        public int? GamingHttpsPort { get; protected set; }

        public string GamingHttpPath { get; protected set; }

        public int? GamingWebRTCPort { get; protected set; }

        public IPEndPoint MasterEndPoint { get; protected set; }

        public ApplicationStatsPublisher AppStatsPublisher { get; protected set; }

        public MasterServerConnection MasterServerConnection { get; protected set; }

        public IPAddress PublicIpAddress { get; protected set; }

        public IPAddress PublicIpAddressIPv6 { get; protected set; }

        public WorkloadController WorkloadController { get; protected set; }

        public virtual GameCache GameCache { get; protected set; }

        public AuthTokenFactory TokenCreator { get; protected set; }

        public S2SCustomTypeCacheMan S2SCacheMan { get; protected set; }

        public int ConnectRetryIntervalSeconds { get; set; }
        #endregion

        #region Properties

        protected bool IsMaster { get; set; }

        private SelfMonitoring selfMonitoring;

        #endregion

        #region Public Methods

        public byte GetCurrentNodeId()
        {
            return this.reader.ReadCurrentNodeId();
        }

        public virtual void OnMasterConnectionEstablished(MasterServerConnectionBase masterServerConnectionBase)
        {
            this.serverStateManager.CheckAppOffline();
        }

        public virtual void OnMasterConnectionFailed(MasterServerConnectionBase masterServerConnection)
        {
        }

        public virtual void OnDisconnectFromMaster(MasterServerConnectionBase masterServerConnection)
        {
        }

        public CustomTypeCache GetS2SCustomTypeCache()
        {
            return this.S2SCacheMan.GetCustomTypeCache();
        }

        public virtual void OnRegisteredAtMaster(MasterServerConnectionBase masterServerConnectionBase, RegisterGameServerResponse registerResponse)
        {
            masterServerConnectionBase.UpdateAllGameStates();
        }

        #endregion

        #region Methods

        private void SetupTokenCreator()
        {
            var sharedKey = Photon.Common.Authentication.Settings.Default.AuthTokenKey;
            if (string.IsNullOrEmpty(sharedKey))
            {
                log.WarnFormat("AuthTokenKey not specified in config. Authentication tokens are not supported============================");
                return;
            }

            var hmacKey = Photon.Common.Authentication.Settings.Default.HMACTokenKey;
            if (string.IsNullOrEmpty(hmacKey))
            {
                log.Warn("HMACTokenKey not specified in config===================================");
            }


            var expirationTimeSeconds = Photon.Common.Authentication.Settings.Default.AuthTokenExpirationS;
            //if (expirationTimeSeconds <= 0)
            //{
            //    log.ErrorFormat("Authentication token expiration to low: expiration={0} seconds", expirationTimeSeconds);
            //}

            var expiration = TimeSpan.FromSeconds(expirationTimeSeconds);
            this.TokenCreator = GetAuthTokenFactory();
            this.TokenCreator.Initialize(sharedKey, hmacKey, expiration, "GS:" + Environment.MachineName);

            log.InfoFormat("TokenCreator intialized with an expiration of {0}==================================", expiration);
        }

        protected virtual AuthTokenFactory GetAuthTokenFactory()
        {
            return new AuthTokenFactory();
        }

        private void UpdateMasterEndPoint()
        {
            IPAddress masterAddress;
            if (!IPAddress.TryParse(GameServerSettings.Default.MasterIPAddress, out masterAddress))
            {
                var hostEntry = Dns.GetHostEntry(GameServerSettings.Default.MasterIPAddress);
                if (hostEntry.AddressList == null || hostEntry.AddressList.Length == 0)
                {
                    throw new ConfigurationException(
                        "MasterIPAddress setting is neither an IP nor an DNS entry: "
                        + GameServerSettings.Default.MasterIPAddress);
                }

                masterAddress =
                    hostEntry.AddressList.First(address => address.AddressFamily == AddressFamily.InterNetwork);

                if (masterAddress == null)
                {
                    throw new ConfigurationException(
                        "MasterIPAddress does not resolve to an IPv4 address! Found: "
                        + string.Join(", ", hostEntry.AddressList.Select(a => a.ToString()).ToArray()));
                }
            }

            int masterPort = GameServerSettings.Default.OutgoingMasterServerPeerPort;
            this.MasterEndPoint = new IPEndPoint(masterAddress, masterPort);
        }

        /// <summary>
        ///   Sanity check to verify that game states are cleaned up correctly
        /// </summary>
        protected virtual void CheckGames()
        {
            var roomNames = this.GameCache.GetRoomNames();

            foreach (var roomName in roomNames)
            {
                Room room;
                if (this.GameCache.TryGetRoomWithoutReference(roomName, out room))
                {
                    room.EnqueueMessage(new RoomMessage((byte)GameMessageCodes.CheckGame));
                }
            }
        }

       
        protected virtual PeerBase CreateGamePeer(InitRequest initRequest)
        {
            var peer = new GameClientPeer(initRequest, this);
            {
                if (this.webRpcManager.IsRpcEnabled)
                {
                    peer.WebRpcHandler = this.webRpcManager.GetWebRpcHandler();
                }
                initRequest.ResponseObject = "ResponseObject";
            }

            log.Info("一个客户端连接中......GameAppliction");


            return peer;
        }

        protected override PeerBase CreatePeer(InitRequest initRequest)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("CreatePeer for {0}", initRequest.ApplicationId);
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat(
                    "incoming game peer at {0}:{1} from {2}:{3}",
                    initRequest.LocalIP,
                    initRequest.LocalPort,
                    initRequest.RemoteIP,
                    initRequest.RemotePort);
            }
            log.Info("一个客户端连接中......");
            return this.CreateGamePeer(initRequest);
        }

        protected virtual void InitLogging()
        {
            //LogManager.SetLoggerFactory(Log4NetLoggerFactory.Instance);
            log4net.GlobalContext.Properties["Photon:ApplicationLogPath"] = Path.Combine(Path.Combine(this.ApplicationRootPath, "bin_win64"),"log");
            log4net.GlobalContext.Properties["LogFileName"] = "GS" + this.ApplicationName;
            FileInfo configFileInfo = new FileInfo(Path.Combine(this.BinaryPath, "log4net.config"));

            if (configFileInfo.Exists)
            {
                LogManager.SetLoggerFactory(Log4NetLoggerFactory.Instance);
                XmlConfigurator.ConfigureAndWatch(configFileInfo);//插件读取插件
            }
        }

        protected override void OnStopRequested()
        {
            log.InfoFormat("OnStopRequested: serverid={0}", ServerId);

            if (this.WorkloadController != null)
            {
                this.WorkloadController.Stop();
            }

            if (this.MasterServerConnection != null)
            {
                this.MasterServerConnection.SendLeaveEventAndWaitForResponse(GameServerSettings.Default.LeaveEventResponseTimeout);
                this.MasterServerConnection.Dispose();
                this.MasterServerConnection = null;
            }

            base.OnStopRequested();
        }

        protected override void Setup()
        {
            this.InitLogging();

            this.S2SCacheMan = new S2SCustomTypeCacheMan();
            log.InfoFormat("Setup: serverId={0}", ServerId);

            Protocol.AllowRawCustomValues = true;
            Protocol.RegisterTypeMapper(new UnknownTypeMapper());

            this.PublicIpAddress = PublicIPAddressReader.ParsePublicIpAddress(GameServerSettings.Default.PublicIPAddress);
            this.PublicIpAddressIPv6 = string.IsNullOrEmpty(GameServerSettings.Default.PublicIPAddressIPv6) ?
                null : IPAddress.Parse(GameServerSettings.Default.PublicIPAddressIPv6);

            this.IsMaster = PublicIPAddressReader.IsLocalIpAddress(this.MasterEndPoint.Address) || this.MasterEndPoint.Address.Equals(this.PublicIpAddress);

            Counter.IsMasterServer.RawValue = this.IsMaster ? 1 : 0;

            this.InitGameCache();

            if (CommonSettings.Default.EnablePerformanceCounters)
            {
                this.InitCorePerformanceCounters();
            }
            else
            {
                log.Info("Performance counters are disabled=======================================");
            }

            this.SetupTokenCreator();
            this.SetupFeedbackControlSystem();
            this.SetupServerStateMonitor();
            this.SetupMasterConnection();

            if (GameServerSettings.Default.AppStatsPublishInterval > 0)
            {
                this.AppStatsPublisher = new ApplicationStatsPublisher(this, GameServerSettings.Default.AppStatsPublishInterval);
            }

            CounterPublisher.DefaultInstance.AddStaticCounterClass(typeof(Hive.Diagnostics.Counter), this.ApplicationName);
            CounterPublisher.DefaultInstance.AddStaticCounterClass(typeof(Counter), this.ApplicationName);

            this.executionFiber = new PoolFiber();
            this.executionFiber.Start();
            this.executionFiber.ScheduleOnInterval(this.CheckGames, 60000, 60000);

            this.selfMonitoring = new SelfMonitoring(
                GameServerSettings.Default.SelfMonitoringSettings,
                GameServerSettings.Default.PublicIPAddress,
                GameServerSettings.Default.GamingTcpPort,
                this.TokenCreator
                );

            this.executionFiber.Schedule(selfMonitoring.StartGames, GameServerSettings.Default.SelfMonitoringDelay);
            this.executionFiber.ScheduleOnInterval(selfMonitoring.Service, GameServerSettings.Default.SelfMonitoringDelay, GameServerSettings.Default.SelfMonitoringInterval);

        }

        private void SetupServerStateMonitor()
        {
            var serverStateFilePath = GameServerSettings.Default.ServerStateFile;

            this.serverStateManager = new ServerStateManager(this.WorkloadController);
            this.serverStateManager.OnNewServerState += OnNewServerState;

            if (string.IsNullOrEmpty(serverStateFilePath) == false)
            {
                this.serverStateManager.Start(Path.Combine(this.ApplicationRootPath, serverStateFilePath));
            }

            if (GameServerSettings.Default.EnableNamedPipe)
            {
                serverStateManager.StartListenPipe();
            }
        }

        protected virtual void SetupMasterConnection()
        {
            if (log.IsInfoEnabled)
            {
                log.Info("Initializing master server connection ...==================================================");
            }

            var masterAddress = GameServerSettings.Default.MasterIPAddress;
            var masterPost = GameServerSettings.Default.OutgoingMasterServerPeerPort;
            this.MasterServerConnection = new MasterServerConnection(this, masterAddress, masterPost, this.ConnectRetryIntervalSeconds);
            this.MasterServerConnection.Initialize();
        }

        private void SetupFeedbackControlSystem()
        {
            var workLoadConfigFile = GameServerSettings.Default.WorkloadConfigFile;

            this.WorkloadController = new WorkloadController(
                this, "_Total", 1000, this.ServerId.ToString(), workLoadConfigFile);

            if (!this.WorkloadController.IsInitialized)
            {
                const string message = "WorkloadController failed to be constructed";

                if (CommonSettings.Default.EnablePerformanceCounters)
                {
                    throw new Exception(message);
                }

                log.Warn(message);
            }

            this.WorkloadController.Start();
        }

        /// <summary>
        /// We need this method here to gracefully skip game cache initialization in VirtualApps
        /// </summary>
        protected virtual void InitGameCache()
        {
            this.GameCache = new GameCache(this);

        }

        protected override void TearDown()
        {
            log.InfoFormat("TearDown: serverId={0}", ServerId);

            if (this.WorkloadController != null)
            {
                this.WorkloadController.Stop();
            }

            if (this.MasterServerConnection != null)
            {
                this.MasterServerConnection.SendLeaveEventAndWaitForResponse(GameServerSettings.Default.LeaveEventResponseTimeout);
                this.MasterServerConnection.Dispose();
                this.MasterServerConnection = null;
            }

            if (this.serverStateManager != null)
            {
                this.serverStateManager.StopListenPipe();
            }

            if (selfMonitoring != null)
            {
                selfMonitoring.StopGames();
            }

            log.Info("服务器关闭了........!");
        }

        protected virtual void OnNewServerState(ServerState oldState, ServerState requestedState, TimeSpan offlineTime)
        {
            switch (requestedState)
            {
                case ServerState.Normal:
                case ServerState.OutOfRotation:
                    if (oldState == ServerState.Offline)
                    {
                        //if (this.MasterServerConnection != null)
                        //{
                        //    var peer = this.MasterServerConnection.GetPeer();
                        //    if (peer != null && peer.IsRegistered)
                        //    {
                        //        this.MasterServerConnection.UpdateAllGameStates();
                        //    }
                        //}
                        //else
                        //{
                        //    log.WarnFormat("Server state is updated but there is not connection to master server");
                        //}
                    }
                    break;

                case ServerState.Offline:
                    this.RaiseOfflineEvent(offlineTime);
                    break;
            }
        }

        protected virtual void RaiseOfflineEvent(TimeSpan time)
        {

        }

        #endregion
    }
}