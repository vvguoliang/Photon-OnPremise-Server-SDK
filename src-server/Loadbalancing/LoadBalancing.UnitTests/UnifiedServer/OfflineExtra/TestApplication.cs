using Photon.Hive;
using Photon.LoadBalancing.GameServer;

namespace Photon.LoadBalancing.UnitTests.UnifiedServer.OfflineExtra
{
    public class TestApplication : GameApplication
    {
        public override GameCache GameCache { get; protected set; }

        public TestApplication()
        {
        }

        protected override void Setup()
        {
            base.Setup();
            this.GameCache = new TestGameCache(this);
        }

        public bool TryGetRoomWithoutReference(string roomId, out TestGameWrapper game)
        {
            game = null;
            Room room;
            if (!this.GameCache.TryGetRoomWithoutReference(roomId, out room))
            {
                return false;
            }

            game = new TestGameWrapper((TestGame) room);
            return true;
        }

        public bool WaitGameDisposed(string gameName, int timeout)
        {
            Room room;
            if (!this.GameCache.TryGetRoomWithoutReference(gameName, out room))
            {
                return true;
            }

            return ((TestGame) room).WaitForDispose(timeout);
        }

        public void SetGamingTcpPort(int port)
        {
            this.GamingTcpPort = port;
        }
}
}
