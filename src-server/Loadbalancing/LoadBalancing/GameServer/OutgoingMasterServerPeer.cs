// --------------------------------------------------------------------------------------------------------------------
// <copyright file="OutgoingMasterServerPeer.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the OutgoingMasterServerPeer type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Photon.Common;
using Photon.Common.LoadBalancer.Common;
using Photon.Common.LoadBalancer.LoadShedding;
using Photon.Common.LoadBalancer.Prediction;
using Photon.LoadBalancing.Common;

namespace Photon.LoadBalancing.GameServer
{
    #region using directives

    using System;
    using System.Net;

    using ExitGames.Logging;
    using Photon.LoadBalancing.ServerToServer.Events;
    using Photon.LoadBalancing.ServerToServer.Operations;
    using Photon.SocketServer;
    using Photon.SocketServer.Diagnostics;
    using Photon.SocketServer.ServerToServer;

    using PhotonHostRuntimeInterfaces;

    using OperationCode = Photon.LoadBalancing.ServerToServer.Operations.OperationCode;

    #endregion

    public class OutgoingMasterServerPeer : OutboundS2SPeer
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly GameApplication application;

        private readonly MasterServerConnectionBase masterServerConnection;

        private bool redirected;

        private IDisposable updateLoop;

        private readonly LoadStatsCollector loadStatsCollector;
        private IDisposable saveStatsLoop;

        private readonly LogCountGuard abortConnectionLogGurad = new LogCountGuard(new TimeSpan(0, 1, 0));

        #endregion

        #region Constructors and Destructors

        public OutgoingMasterServerPeer(MasterServerConnectionBase masterServerConnection)
            : base(masterServerConnection.Application)
        {
            this.masterServerConnection = masterServerConnection;
            this.application = masterServerConnection.Application;            
            this.SetPrivateCustomTypeCache(this.application.GetS2SCustomTypeCache());
            if (CommonSettings.Default.UseLoadPrediction)
            {
                var configFile = GameServerSettings.Default.PredictionConfigFile;
                var applicationRootPath = masterServerConnection.Application.ApplicationRootPath;
                this.loadStatsCollector = new LoadStatsCollector(applicationRootPath, configFile, GameServerSettings.Default.PredictionFactor);
            }
        }

        #endregion

        #region Properties

        public bool IsRegistered { get; protected set; }

        #endregion

        #region Public Methods

        public virtual Dictionary<byte, int[]> GetPredictionData()
        {
            return this.loadStatsCollector != null ? this.loadStatsCollector.GetPredictionData() : null;
        }

        public static byte[] GetSupportedProtocolsFromString(string supportedProtocols)
        {
            var protocols = supportedProtocols.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            var result = new List<byte>();

            foreach (var protocol in protocols)
            {
                NetworkProtocolType protocolId;
                if (Enum.TryParse<NetworkProtocolType>(protocol, true, out protocolId))
                {
                    result.Add((byte)protocolId);
                }
                else
                {
                    log.WarnFormat("Unknown protocol name:{0}", protocol);
                }
            }

            return result.Count == 0 ? null : result.ToArray();
        }

        #endregion

        #region Methods

        private void HandleRegisterGameServerResponse(OperationResponse operationResponse)
        {
            var contract = new RegisterGameServerResponse(this.Protocol, operationResponse);
            this.HandleRegisterGameServerResponse(operationResponse.ReturnCode, operationResponse.DebugMessage, contract);
        }

        private void HandleRegisterGameServerResponse(short returnCode, string debugMessage, RegisterGameServerResponse response)
        {
            switch (returnCode)
            {
                case (short)ErrorCode.Ok:
                    {
                        if (!response.IsValid)
                        {
                            log.Error("RegisterGameServerInitResponse contract invalid: " + response.GetErrorMessage());
                            this.Disconnect();
                            return;
                        }

                        log.InfoFormat("Successfully registered at master server: serverId={0}", this.application.ServerId);
                        this.IsRegistered = true;
                        this.StartUpdateLoop();
                        this.StartSaveStatsLoop();
                        this.OnRegisteredAtMaster(response);
                        break;
                    }

                default:
                    {
                        log.ErrorFormat(this.masterServerConnection + ": Failed to register at master: err={0}, msg={1}, serverid={2}", returnCode,
                            debugMessage, this.application.ServerId);
                        this.Disconnect();
                        break;
                    }
            }

        }

        private void OnRegisteredAtMaster(RegisterGameServerResponse registerResponse)
        {
            this.masterServerConnection.OnRegisteredAtMaster(registerResponse);
        }

        protected override void OnDisconnect(DisconnectReason reasonCode, string reasonDetail)
        {
            this.IsRegistered = false;
            this.StopUpdateLoop();
            this.StopSaveStatsLoop();

            if (this.loadStatsCollector != null)
            {
                this.loadStatsCollector.SaveToFile();
            }
            // if RegisterGameServerResponse tells us to connect somewhere else we don't need to reconnect here
            if (this.redirected)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("{0} disconnected from master server: reason={1}, detail={2}, serverId={3}", this.ConnectionId, reasonCode, reasonDetail, this.application.ServerId);
                }
            }
            else
            {
                log.WarnFormat(this.masterServerConnection.Name + ": connection closed (id={0}, reason={1}, detail={2}), serverId={3}", this.ConnectionId, reasonCode, reasonDetail, this.application.ServerId);
                this.masterServerConnection.OnDisconnect(reasonCode, reasonDetail);
            }
        }

        protected override void OnEvent(IEventData eventData, SendParameters sendParameters)
        {
        }

        protected override void OnOperationRequest(OperationRequest request, SendParameters sendParameters)
        {

            Dictionary<byte, object> dict = request.Parameters;
            foreach (object value in dict.Values)
            {
                log.Info("============OutgoingMasterServerPeer==========:" + value.ToString());
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Received unknown operation code {0}", request.OperationCode);
            }

            var response = new OperationResponse
            {
                OperationCode = request.OperationCode, 
                ReturnCode = (short)ErrorCode.InternalServerError, 
                DebugMessage = LBErrorMessages.UnknownOperationCode,
            };
            this.SendOperationResponse(response, sendParameters);
        }
        
        protected override void OnOperationResponse(OperationResponse operationResponse, SendParameters sendParameters)
        {
            try
            {
                switch ((OperationCode)operationResponse.OperationCode)
                {
                    default:
                        {
                            if (log.IsDebugEnabled)
                            {
                                log.DebugFormat("Received unknown operation code {0}", operationResponse.OperationCode);
                            }

                            break;
                        }

                    case OperationCode.RegisterGameServer:
                        {
                            this.HandleRegisterGameServerResponse(operationResponse);
                            break;
                        }
                }
            }
            catch(Exception ex)
            {
                log.Error(ex);
            }
        }

        private void Register()
        {
            var contract = new RegisterGameServer
            {
                GameServerAddress = this.application.PublicIpAddress.ToString(),
                GameServerHostName = GameServerSettings.Default.PublicHostName,

                UdpPort = GameServerSettings.Default.RelayPortUdp == 0 ? this.application.GamingUdpPort : GameServerSettings.Default.RelayPortUdp + this.application.GetCurrentNodeId() - 1,
                TcpPort = GameServerSettings.Default.RelayPortTcp == 0 ? this.application.GamingTcpPort : GameServerSettings.Default.RelayPortTcp + this.application.GetCurrentNodeId() - 1,
                WebSocketPort = GameServerSettings.Default.RelayPortWebSocket == 0 ? this.application.GamingWebSocketPort : GameServerSettings.Default.RelayPortWebSocket + this.application.GetCurrentNodeId() - 1,
                SecureWebSocketPort = GameServerSettings.Default.RelayPortSecureWebSocket == 0 ? this.application.GamingSecureWebSocketPort : GameServerSettings.Default.RelayPortSecureWebSocket + this.application.GetCurrentNodeId() - 1,
                HttpPort = GameServerSettings.Default.RelayPortHttp == 0 ? this.application.GamingHttpPort : GameServerSettings.Default.RelayPortHttp + this.application.GetCurrentNodeId() - 1,
                SecureHttpPort = this.application.GamingHttpsPort,
                WebRTCPort = GameServerSettings.Default.GamingWebRTCPort,
                HttpPath = this.application.GamingHttpPath,
                ServerId = this.application.ServerId.ToString(),
                ServerState = (int)this.application.WorkloadController.ServerState,
                LoadLevelCount = (byte)FeedbackLevel.LEVELS_COUNT,
                PredictionData = this.GetPredictionData(),
                LoadBalancerPriority = GameServerSettings.Default.LoadBalancerPriority,
                LoadIndex = (byte)this.application.WorkloadController.FeedbackLevel,
                SupportedProtocols = GetSupportedProtocolsFromString(GameServerSettings.Default.SupportedProtocols),
                };

            if (this.application.PublicIpAddressIPv6 != null)
            {
                contract.GameServerAddressIPv6 = this.application.PublicIpAddressIPv6.ToString();
            }

            if (log.IsInfoEnabled)
            {
                log.InfoFormat(
                    "Registering game server with address {0}, TCP {1}, UDP {2}, WebSocket {3}, Secure WebSocket {4}, HTTP {5}, ServerID {6}, Hostname {7}, IPv6Address {8}",
                    contract.GameServerAddress,
                    contract.TcpPort,
                    contract.UdpPort,
                    contract.WebSocketPort,
                    contract.SecureWebSocketPort,
                    contract.HttpPort,
                    contract.ServerId,
                    contract.GameServerHostName,
                    contract.GameServerAddressIPv6);
            }

            var request = new OperationRequest((byte)OperationCode.RegisterGameServer, contract);
            this.SendOperationRequest(request, new SendParameters());
        }

        private void StartUpdateLoop()
        {
            if (this.updateLoop != null)
            {
                log.Error("Update Loop already started! Duplicate RegisterGameServer response?");
                this.updateLoop.Dispose();
            }

            this.updateLoop = this.RequestFiber.ScheduleOnInterval(this.UpdateServerState, 1000, 1000);
            this.application.WorkloadController.FeedbacklevelChanged += this.WorkloadController_OnFeedbacklevelChanged;
        }

        private void StopUpdateLoop()
        {
            if (this.updateLoop != null)
            {
                this.updateLoop.Dispose();
                this.updateLoop = null;

                this.application.WorkloadController.FeedbacklevelChanged -= this.WorkloadController_OnFeedbacklevelChanged;
            }
        }

        private void StartSaveStatsLoop()
        {
            if (this.loadStatsCollector == null)
            {
                return;
            }

            if (this.saveStatsLoop != null)
            {
                log.Error("Update Loop already started! Duplicate RegisterGameServer response?");
                this.updateLoop.Dispose();
            }

            var interval = 60000 * GameServerSettings.Default.LoadStatsSaveIntervalMinute;
            this.saveStatsLoop = this.RequestFiber.ScheduleOnInterval(this.loadStatsCollector.SaveToFile, interval, interval);
        }

        private void StopSaveStatsLoop()
        {
            if (this.loadStatsCollector == null)
            {
                return;
            }

            if (this.saveStatsLoop != null)
            {
                this.saveStatsLoop.Dispose();
                this.saveStatsLoop = null;
            }
        }

        private void UpdateServerState(FeedbackLevel workload, int peerCount, ServerState state)
        {
            Dictionary<byte, int[]> predictionData = null;
            if (this.loadStatsCollector != null)
            {
                this.loadStatsCollector.UpdatePrediction(peerCount, workload, out predictionData);
            }

            if (!this.IsRegistered)
            {
                return;
            }

            var contract = new UpdateServerEvent
            {
                LoadIndex = (byte)workload,
                PeerCount = peerCount,
                State = (int)state,
                LoadLevelsCount = (int)FeedbackLevel.LEVELS_COUNT,
                PredictionData = predictionData,
            };
            var eventData = new EventData((byte)ServerEventCode.UpdateServer, contract);
            this.SendEvent(eventData, new SendParameters());
        }

        private void UpdateServerState()
        {
            if (this.Connected == false)
            {
                return;
            }

            this.UpdateServerState(
                this.application.WorkloadController.FeedbackLevel,
                this.application.PeerCount,
                this.application.WorkloadController.ServerState);
        }

        private void WorkloadController_OnFeedbacklevelChanged(object sender, EventArgs e)
        {
            this.UpdateServerState();
        }

        protected override void OnSendBufferFull()
        {
            log.WarnFormat(this.abortConnectionLogGurad,
                this.masterServerConnection.Name + ": connection aborted {0}, IP {1}: SendBufferFull", this.ConnectionId, this.RemoteIP);
            this.AbortConnection();
        }

        protected override void OnConnectionEstablished(object responseObject)
        {
            this.masterServerConnection.OnConnectionEstablished();

            if (responseObject == null)
            {
                if (log.IsInfoEnabled)
                {
                    log.Info($"Init Response object is null. Use operation request to register on master");
                }

                this.RequestFiber.Enqueue(this.Register);
                return;
            }

            var response = new RegisterGameServerInitResponse(this.Protocol, (Dictionary<byte, object>)responseObject);
            this.HandleRegisterGameServerResponse(response.ReturnCode, response.DebugMessage, response);
        }

        protected override void OnConnectionFailed(int errorCode, string errorMessage)
        {
            this.masterServerConnection.OnConnectionFailed(errorCode, errorMessage);
        }

        #endregion
    }
}