using System;
using System.Threading;
using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;
using Photon.Common.Authentication.Data;
using PhotonCloud.Authentication.CustomAuth;

namespace PhotonCloud.NameServer.VirtualApps
{
    public class NSVirtualApp : IVACustomAuthCounters
    {
        #region Consts and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private IDisposable updateStatsTimer;

        private readonly PoolFiber fiber = new PoolFiber();
        
        private readonly string applicationId;
        private int statsChanged;

        #endregion

        #region .ctr

        public NSVirtualApp(string applicationId)
        {
            this.applicationId = applicationId;
            this.fiber.Start();
        }

        #endregion

        #region Properties

        public long LastUpdateTime { get; private set; }

        #endregion

        #region Interface implementations

        #region IVACustomAuthCounters

        public void IncrementCustomAuthQueueFullErrors()
        {
        }

        public void IncrementCustomAuthQueueTimeouts()
        {
        }


        public void IncrementCustomAuthHttpRequests(long ticks)
        {
        }

        public void IncrementCustomAuthResultsData()
        {
        }

        public void IncrementCustomAuthResultsAccepted()
        {
        }

        public void IncrementCustomAuthResultsDenied()
        {
        }

        public void IncrementCustomAuthHttpErrors(ClientAuthenticationType clientAuthType)
        {

        }

        public void IncrementCustomAuthHttpTimeouts(ClientAuthenticationType clientAuthType)
        {
        }

        public void IncrementCustomAuthErrors(ClientAuthenticationType clientAuthType)
        {
        }

        #endregion

        #endregion

        #region Methods

        private void PublishStats()
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Stats update is published for app:{0}", this.applicationId);
            }

            this.updateStatsTimer = null;

            if (Interlocked.Exchange(ref this.statsChanged, 0) == 0)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("there is no changes in stats. no event sent. app:{0}", this.applicationId);
                }
                return;
            }

            //var @event = new UpdateApplicationStatsEvent
            //    {
            //        ApplicationId = this.ApplicationKey.ApplicationId,
            //        ApplicationVersion = this.ApplicationKey.ApplicationVersion,
            //        PlayerCount = this.peerCount,
            //        GameCount = this.gameCount,
            //        MessageCount = msgCount,
            //        SendReceivedBytes = bytes,

            //        TcpPeersDisconnectedByClientCount = tcpPeersDisconnectedByClient,
            //        TcpPeersDisconnectedByManagedCount = tcpPeersDisconnectedByManaged,
            //        TcpPeersDisconnectedByServerCount = tcpPeersDisconnectedByServer,
            //        TcpPeersDisconnectedByTimeoutCount = tcpPeersDisconnectedByTimeout,

            //        UdpPeersDisconnectedByClientCount = udpPeersDisconnectedByClient,
            //        UdpPeersDisconnectedByManagedCount = udpPeersDisconnectedByManaged,
            //        UdpPeersDisconnectedByServerCount = udpPeersDisconnectedByServer,
            //        UdpPeersDisconnectedByTimeoutCount = udpPeersDisconnectedByTimeout,
            //        EcSliceCount = ecSlice,
            //        EcTotalEventsCount = ecTotalEvents,
            //        WebHooksHttpSuccessCount = webHooksHttpSuccess,
            //        WebHooksHttpErrorsCount = webHooksHttpErrors,
            //        WebHooksHttpTimeoutCount = webHooksHttpTimeout,
            //        WebHooksQueueSuccessCount = webHooksQueueSuccess,
            //        WebHooksQueueErrorsCount = webHooksQueueErrors,
            //        WebHooksHttpRequestExecTime = webHooksHttpRequestExecTime,
            //        OldAuthPeers = this.oldAuthPeerCount,
            //        NoTokenAuthOnGS = this.noTokenAuthOnGSPeerCount,
            //    };

            //var data = new EventData((byte)ServerEventCode.UpdateAppStats, @event);
            //this.MasterServerConnection.SendEventIfRegistered(data, new SendParameters());
        }

        #endregion
    }
}
