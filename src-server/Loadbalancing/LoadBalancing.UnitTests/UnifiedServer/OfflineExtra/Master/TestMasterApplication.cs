using ExitGames.Logging;
using Photon.LoadBalancing.MasterServer;
using Photon.LoadBalancing.MasterServer.GameServer;

namespace Photon.LoadBalancing.UnitTests.UnifiedServer.OfflineExtra.Master
{
    public interface ITestMasterApplication
    {
        int OnBeginReplicationCount { get; }
        int OnFinishReplicationCount { get; }
        int OnStopReplicationCount { get; }
        int OnServerWentOfflineCount { get; }
        void ResetStats();
    }

    public class TestMasterApplication : MasterApplication, ITestMasterApplication
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        #region Properties

        public int OnBeginReplicationCount { get { return ((TestGameApplication)this.DefaultApplication).OnBeginReplicationCount; } }

        public int OnFinishReplicationCount { get { return ((TestGameApplication) this.DefaultApplication).OnFinishReplicationCount; } }

        public int OnStopReplicationCount { get { return ((TestGameApplication) this.DefaultApplication).OnStopReplicationCount; } }

        public int OnServerWentOfflineCount { get; private set; }

        #endregion

        #region Public

        public override void OnServerWentOffline(GameServerContext gameServerContext)
        {
            base.OnServerWentOffline(gameServerContext);
            ++this.OnServerWentOfflineCount;
        }

        public void ResetStats()
        {
            this.OnServerWentOfflineCount = 0;
            ((TestGameApplication) this.DefaultApplication).ResetStats();
            log.DebugFormat("Stats are reset");
        }
        #endregion

        #region Privates

        protected override void Initialize()
        {
            base.Initialize();

            this.DefaultApplication = new TestGameApplication("{Default}", "{Default}", this.LoadBalancer);
        }

        #endregion
    }
}
