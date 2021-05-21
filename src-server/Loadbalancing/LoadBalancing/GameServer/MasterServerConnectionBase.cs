
using System.Collections.Generic;
using ExitGames.Configuration;
using Photon.Common.LoadBalancer.LoadShedding;
using Photon.LoadBalancing.ServerToServer.Operations;
using Photon.SocketServer.Diagnostics;
using PhotonHostRuntimeInterfaces;

namespace Photon.LoadBalancing.GameServer
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;

    using ExitGames.Logging;

    using Photon.SocketServer;

    public abstract class MasterServerConnectionBase : IDisposable
    {
        #region Fields and Constants

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly int connectRetryIntervalSeconds;

        private int isReconnecting;

        private Timer reconnectTimer;

        private OutgoingMasterServerPeer peer;

        private readonly TimeIntervalCounter reconnectsCount = new TimeIntervalCounter(new TimeSpan(0, GameServerSettings.Default.ReconnectsCountPerMinutes, 0), 10);

        private const string name = "s2s MS connection";

        protected readonly string ConnectionId = Guid.NewGuid().ToString();

        private readonly LogCountGuard connectionFailedGuard = new LogCountGuard(new TimeSpan(0, 1, 0));//once per minute
        #endregion

        #region .ctr

        protected MasterServerConnectionBase(GameApplication controller, string address, int port, int connectRetryIntervalSeconds)
        {
            this.Application = controller;
            this.Address = address;
            this.Port = port;
            this.connectRetryIntervalSeconds = connectRetryIntervalSeconds;
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("ConnectionId={0}", this.ConnectionId);
            }
        }

        #endregion

        #region Properties

        public GameApplication Application { get; private set; }

        public string Address { get; private set; }

        public IPEndPoint EndPoint { get; private set; }

        public int Port { get; private set; }

        public bool IsReconnecting
        {
            get { return Interlocked.CompareExchange(ref this.isReconnecting, 0, 0) != 0; }
        }

        public int ReconnectsCount
        {
            get { return this.reconnectsCount.Value; }
        }

        public string Name { get { return name; } }
        #endregion

        #region Publics

        public OutgoingMasterServerPeer GetPeer()
        {
            return this.peer;
        }

        public void Initialize()
        {
            this.ConnectToMaster();
        }

        public SendResult SendEventIfRegistered(IEventData eventData, SendParameters sendParameters)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Sending event to master. EventCode={0}, ConnectionId={1}", eventData.Code, this.ConnectionId);
            }

            var masterPeer = this.peer;
            if (masterPeer == null || masterPeer.IsRegistered == false)
            {
                if (log.IsDebugEnabled)
                {
                    if (masterPeer != null)
                    {
                        log.DebugFormat("Event data was not sent. peer is unregistered. Event Code={0}, IsRegistered:{1}, ConnectionId:{2}",
                            eventData.Code, masterPeer.IsRegistered, this.ConnectionId);
                    }
                    else
                    {
                        log.DebugFormat("Event data was not sent. peer is null. Event Code={0}, ConnectionId:{1}", 
                            eventData.Code, this.ConnectionId);
                    }

                }
                return SendResult.Disconnected;
            }

            return masterPeer.SendEvent(eventData, sendParameters);
        }

        public SendResult SendEvent(IEventData eventData, SendParameters sendParameters)
        {
            var masterPeer = this.peer;
            if (masterPeer == null || masterPeer.Connected == false)
            {
                return SendResult.Disconnected;
            }

            return masterPeer.SendEvent(eventData, sendParameters);
        }

        public virtual void UpdateAllGameStates()
        {
        }

        public void ConnectToMaster(IPEndPoint endPoint)
        {
            if (this.Application.Running == false)
            {
                return;
            }

            if (this.peer == null)
            {
                this.peer = this.CreateServerPeer();
            }

            if (this.peer.ConnectTcp(endPoint, "Master", this.GetInitObject()))
            {
                if (log.IsInfoEnabled)
                {
                    log.InfoFormat("Connecting to master at {0}, serverId={1}", endPoint, this.Application.ServerId);
                }
            }
            else
            {
                log.WarnFormat("master connection refused - is the process shutting down ? {0}", this.Application.ServerId);
            }
        }

        public virtual void OnConnectionEstablished()
        {
            if (log.IsInfoEnabled)
            {
                log.InfoFormat("Master connection established: address:{0}, ConnectionId:{1}", 
                    this.Address, this.ConnectionId);
            }
            Interlocked.Exchange(ref this.isReconnecting, 0);
            this.Application.OnMasterConnectionEstablished(this);
        }

        public virtual void OnConnectionFailed(int errorCode, string errorMessage)
        {
            if (this.isReconnecting == 0)
            {
                log.ErrorFormat(name + 
                    ": Master connection failed: address={0}, errorCode={1}, msg={2}",
                    this.EndPoint,
                    errorCode,
                    errorMessage);
            }
            else if (log.IsWarnEnabled)
            {
                log.WarnFormat(this.connectionFailedGuard, 
                    name + 
                    ": Master connection failed: address={0}, errorCode={1}, msg={2}",
                    this.EndPoint,
                    errorCode,
                    errorMessage);
            }

            this.ReconnectToMaster();

            this.Application.OnMasterConnectionFailed(this);
        }

        public virtual void OnDisconnect(DisconnectReason reasonCode, string reasonDetail)
        {
            this.ReconnectToMaster();
            this.Application.OnDisconnectFromMaster(this);
        }

        public void Dispose()
        {
            var timer = this.reconnectTimer;
            if (timer != null)
            {
                timer.Dispose();
                this.reconnectTimer = null;
            }

            var masterPeer = this.peer;
            if (masterPeer != null)
            {
                masterPeer.Disconnect();
                masterPeer.Dispose();
                this.peer = null;
            }
        }

        public void OnRegisteredAtMaster(RegisterGameServerResponse registerResponse)
        {
            if (log.IsInfoEnabled)
            {
                log.InfoFormat("Master connection registered on master: address:{0}", this.Address);
            }
            this.Application.OnRegisteredAtMaster(this, registerResponse);
        }

        #endregion

        #region Privates

        protected abstract OutgoingMasterServerPeer CreateServerPeer();

        private Dictionary<byte, object> GetInitObject()
        {
            var contract = new RegisterGameServerDataContract
            {
                GameServerAddress = this.Application.PublicIpAddress.ToString(),
                GameServerHostName = GameServerSettings.Default.PublicHostName,

                UdpPort = GameServerSettings.Default.RelayPortUdp == 0 ? this.Application.GamingUdpPort : GameServerSettings.Default.RelayPortUdp + this.Application.GetCurrentNodeId() - 1,
                TcpPort = GameServerSettings.Default.RelayPortTcp == 0 ? this.Application.GamingTcpPort : GameServerSettings.Default.RelayPortTcp + this.Application.GetCurrentNodeId() - 1,
                WebSocketPort = GameServerSettings.Default.RelayPortWebSocket == 0 ? this.Application.GamingWebSocketPort : GameServerSettings.Default.RelayPortWebSocket + this.Application.GetCurrentNodeId() - 1,
                SecureWebSocketPort = GameServerSettings.Default.RelayPortSecureWebSocket == 0 ? this.Application.GamingSecureWebSocketPort : GameServerSettings.Default.RelayPortSecureWebSocket + this.Application.GetCurrentNodeId() - 1,
                HttpPort = GameServerSettings.Default.RelayPortHttp == 0 ? this.Application.GamingHttpPort : GameServerSettings.Default.RelayPortHttp + this.Application.GetCurrentNodeId() - 1,
                SecureHttpPort = this.Application.GamingHttpsPort,
                WebRTCPort = GameServerSettings.Default.GamingWebRTCPort,
                HttpPath = this.Application.GamingHttpPath,
                ServerId = this.Application.ServerId.ToString(),
                ServerState = (int)this.Application.WorkloadController.ServerState,
                LoadLevelCount = (byte)FeedbackLevel.LEVELS_COUNT,
                PredictionData = this.GetPeer().GetPredictionData(),
                LoadBalancerPriority = GameServerSettings.Default.LoadBalancerPriority,
                LoadIndex = (byte)this.Application.WorkloadController.FeedbackLevel,
                SupportedProtocols = OutgoingMasterServerPeer.GetSupportedProtocolsFromString(GameServerSettings.Default.SupportedProtocols),

            };

            if (this.Application.PublicIpAddressIPv6 != null)
            {
                contract.GameServerAddressIPv6 = this.Application.PublicIpAddressIPv6.ToString();
            }

            if (log.IsInfoEnabled)
            {
                log.InfoFormat(
                    "Connecting to master server with address {0}, TCP {1}, UDP {2}, WebSocket {3}, " +
                    "Secure WebSocket {4}, HTTP {5}, ServerID {6}, Hostname {7}, IPv6Address {8}, WebRTC {9}",
                    contract.GameServerAddress,
                    contract.TcpPort,
                    contract.UdpPort,
                    contract.WebSocketPort,
                    contract.SecureWebSocketPort,
                    contract.HttpPort,
                    contract.ServerId,
                    contract.GameServerHostName,
                    contract.GameServerAddressIPv6,
                    contract.WebRTCPort);
            }

            return contract.ToDictionary();
        }

        private void ConnectToMaster()
        {
            if (this.reconnectTimer != null)
            {
                this.reconnectTimer.Dispose();
                this.reconnectTimer = null;
            }

            // check if the photon application is shuting down
            if (this.Application.Running == false)
            {
                return;
            }

            this.reconnectsCount.Increment(1);

            try
            {
                this.UpdateEndpoint();
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("MasterServer endpoint for address {0} updated to {1}", this.Address, this.EndPoint);
                }

                this.ConnectToMaster(this.EndPoint);
            }
            catch (Exception e)
            {
                log.Error(e);
                if (this.isReconnecting == 1)
                {
                    this.ReconnectToMaster();
                }
                else
                {
                    throw;
                }
            }
        }

        private void UpdateEndpoint()
        {
            IPAddress masterAddress;
            if (!IPAddress.TryParse(this.Address, out masterAddress))
            {
                var hostEntry = Dns.GetHostEntry(this.Address);
                if (hostEntry.AddressList == null || hostEntry.AddressList.Length == 0)
                {
                    throw new ConfigurationException(
                        "MasterIPAddress setting is neither an IP nor an DNS entry: " + this.Address);
                }

                masterAddress = hostEntry.AddressList.First(address => address.AddressFamily == AddressFamily.InterNetwork);

                if (masterAddress == null)
                {
                    throw new ConfigurationException(
                        "MasterIPAddress does not resolve to an IPv4 address! Found: "
                        + string.Join(", ", hostEntry.AddressList.Select(a => a.ToString()).ToArray()));
                }
            }

            this.EndPoint = new IPEndPoint(masterAddress, this.Port);
        }

        protected virtual void ReconnectToMaster()
        {
            if (this.Application.Running == false)
            {
                return;
            }

            if (this.reconnectTimer != null)
            {
                return;
            }

            Interlocked.Exchange(ref this.isReconnecting, 1);
            this.reconnectTimer = new Timer(o => this.ConnectToMaster(), null, this.connectRetryIntervalSeconds * 1000, Timeout.Infinite);
        }

        #endregion
    }
}
