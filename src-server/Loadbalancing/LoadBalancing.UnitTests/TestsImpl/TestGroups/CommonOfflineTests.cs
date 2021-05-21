using System.Reflection;
using System.Threading;
using Photon.Realtime;
using NUnit.Framework;
using Photon.Hive.Operations;
using Photon.LoadBalancing.GameServer;
using Photon.LoadBalancing.MasterServer;
using Photon.LoadBalancing.UnifiedClient;
using Photon.LoadBalancing.UnitTests.UnifiedServer.OfflineExtra.Master;
using Photon.LoadBalancing.UnitTests.UnifiedServer.Policy;
using ErrorCode = Photon.Realtime.ErrorCode;

namespace Photon.LoadBalancing.UnitTests.UnifiedTests
{
    public abstract partial class LBApiTestsImpl
    {
        #region Common Offline Tests

        [Test]
        public void TestReplicationAfterShortConnectionBreak()
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }
            var connPolicy = (OfflineConnectPolicy)this.connectPolicy;

            var masterServer = (ITestMasterApplication)connPolicy.MasterServer.Application;
            Thread.Sleep(1000);// to finish everything from perv tests
            masterServer.ResetStats();

            try
            {
                connPolicy.MasterServer.SimulateConnectionBreak(5000);

                Thread.Sleep(GameServerSettings.Default.ConnectReytryInterval * 1000 + 2000);

                Assert.That(masterServer.OnBeginReplicationCount, Is.EqualTo(2));
                Assert.That(masterServer.OnFinishReplicationCount, Is.EqualTo(2));
                Assert.That(masterServer.OnStopReplicationCount, Is.EqualTo(0));
                Assert.That(masterServer.OnServerWentOfflineCount, Is.EqualTo(0));
            }
            finally
            {
                masterServer.ResetStats();
            }
        }

        [Test]
        public void TestLongConnectionBreak()
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }


            Thread.Sleep(1000);// to finish everything from perv tests

            var connPolicy = (OfflineConnectPolicy)this.connectPolicy;

            var masterServer = (ITestMasterApplication)connPolicy.MasterServer.Application;
            masterServer.ResetStats();

            try
            {
                connPolicy.MasterServer.SimulateConnectionBreak((uint)GameServerSettings.Default.ConnectReytryInterval * 1000 + 1000);

                Thread.Sleep(MasterServerSettings.Default.GSContextTTL + 1000);

                Assert.That(masterServer.OnBeginReplicationCount, Is.EqualTo(0));
                Assert.That(masterServer.OnFinishReplicationCount, Is.EqualTo(0));
                Assert.That(masterServer.OnStopReplicationCount, Is.EqualTo(0));
                Assert.That(masterServer.OnServerWentOfflineCount, Is.EqualTo(2));
            }
            finally
            {
                masterServer.ResetStats();
                Thread.Sleep(GameServerSettings.Default.ConnectReytryInterval * 1000);
            }
        }

        [Test]
        public void TestConnectionLost()
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }

            Thread.Sleep(1000);// to finish everything from perv tests

            var connPolicy = (OfflineConnectPolicy)this.connectPolicy;

            var masterServer = (ITestMasterApplication)connPolicy.MasterServer.Application;

            try
            {
                connPolicy.GameServer.Stop();

                Thread.Sleep(1000);

                Assert.That(masterServer.OnServerWentOfflineCount, Is.EqualTo(1));
            }
            finally
            {
                connPolicy.GameServer.Start();
                masterServer.ResetStats();
            }
        }

        [Test]
        public void TestGameAvilabilityAfterConnectionBreak()
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }

            Thread.Sleep(1000);// to finish everything from perv tests

            var connPolicy = (OfflineConnectPolicy)this.connectPolicy;

            var masterServer = (ITestMasterApplication)connPolicy.MasterServer.Application;
            masterServer.ResetStats();

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            try
            {
                string roomName = this.GenerateRandomizedRoomName("TestGameAvilabilityAfterConnectionBreak_");
                masterClient1 = this.CreateGameOnGameServer(this.Player1, roomName, null, 0, true, true, 3, null, null);

                connPolicy.MasterServer.SimulateConnectionBreak(1000);

                Thread.Sleep(1100);

                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                masterClient2.JoinGame(roomName, ErrorCode.Ok);

                Thread.Sleep(GameServerSettings.Default.ConnectReytryInterval * 1000);

                // check that game still there after restoring of connection
                masterClient2.JoinGame(roomName, ErrorCode.Ok);

                Assert.That(masterServer.OnBeginReplicationCount, Is.EqualTo(2));
                Assert.That(masterServer.OnFinishReplicationCount, Is.EqualTo(2));
                Assert.That(masterServer.OnStopReplicationCount, Is.EqualTo(0));
                Assert.That(masterServer.OnServerWentOfflineCount, Is.EqualTo(0));
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
                masterServer.ResetStats();
            }
        }

        [Test]
        public void TestGameCreatedOnGSReplicatedToMS()
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }

            Thread.Sleep(1000);// to finish everything from perv tests

            var connPolicy = (OfflineConnectPolicy)this.connectPolicy;

            var masterServer = (ITestMasterApplication)connPolicy.MasterServer.Application;
            masterServer.ResetStats();

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            try
            {
                string roomName = this.GenerateRandomizedRoomName("TestGameCreatedOnGSReplicatedToMS_");

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                this.ConnectAndAuthenticate(masterClient1, this.GameServerAddress);

                connPolicy.MasterServer.SimulateConnectionBreak(10000);

                masterClient1.CreateGame(roomName);

                Thread.Sleep(GameServerSettings.Default.ConnectReytryInterval * 1000 + 1000);

                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                masterClient2.JoinGame(roomName, ErrorCode.Ok);

                Thread.Sleep(1000);
                Assert.That(masterServer.OnBeginReplicationCount, Is.EqualTo(2));
                Assert.That(masterServer.OnFinishReplicationCount, Is.EqualTo(2));
                Assert.That(masterServer.OnStopReplicationCount, Is.EqualTo(0));
                Assert.That(masterServer.OnServerWentOfflineCount, Is.EqualTo(0));
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
                masterServer.ResetStats();
            }
        }

        [Test]
        public void TestRemoveNonReplicatedGames()
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }

            Thread.Sleep(1000);// to finish everything from perv tests

            var connPolicy = (OfflineConnectPolicy)this.connectPolicy;

            var masterServer = (ITestMasterApplication)connPolicy.MasterServer.Application;
            masterServer.ResetStats();

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                masterClient1 = this.CreateGameOnGameServer(this.Player1, roomName, null, 0, true, true, 3, null, null);

                connPolicy.MasterServer.SimulateConnectionBreak((uint)GameServerSettings.Default.ConnectReytryInterval * 1000 - 1000);

                masterClient1.Disconnect(); // after disconnect game should be removed from GS

                // wait till game server reconnts
                Thread.Sleep(GameServerSettings.Default.ConnectReytryInterval * 1000 + 1000);

                // join 2nd client on master: 
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                masterClient2.JoinGame(roomName, ErrorCode.GameDoesNotExist);

                Assert.That(masterServer.OnBeginReplicationCount, Is.EqualTo(2));
                Assert.That(masterServer.OnFinishReplicationCount, Is.EqualTo(2));
                Assert.That(masterServer.OnStopReplicationCount, Is.EqualTo(0));
                Assert.That(masterServer.OnServerWentOfflineCount, Is.EqualTo(0));
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
                connPolicy.GameServer.Stop();
                connPolicy.GameServer.Start();
                masterServer.ResetStats();
            }
        }

        [Test]
        public void TestCleanupPlayerCacheAfterServerLoss()
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }

            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("this tests requires userId to be set");
            }

            Thread.Sleep(1000);// to finish everything from perv tests

            var connPolicy = (OfflineConnectPolicy)this.connectPolicy;

            var masterServer = (ITestMasterApplication)connPolicy.MasterServer.Application;
            masterServer.ResetStats();

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                masterClient1 = this.CreateGameOnGameServer(this.Player1, roomName, null, 0, true, true, 3, null, null, checkUserOnJoin: true);

                const int waitDelta = 3_000;

                // we stop both because we do not know where game was created
                connPolicy.GameServer.Stop();
                connPolicy.GameServer2.Stop();

                // wait till game server context will be removed
                Thread.Sleep(MasterServerSettings.Default.GSContextTTL + waitDelta);

                // join 2nd client on master: 
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // gs is not reconnected yet, so we will not find our player in cache
                masterClient2.FindFriends(new string[] { this.Player1 }, out var onlineStates, out var userStates);

                Assert.That(onlineStates[0], Is.False);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
                connPolicy.GameServer.Start();
                connPolicy.GameServer2.Start();
                masterServer.ResetStats();
            }
        }

        [Test]
        public void TestCleanupPlayerCacheAfterLongConnectionBreak()
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }

            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("this tests requires userId to be set");
            }

            Thread.Sleep(1000);// to finish everything from perv tests

            var connPolicy = (OfflineConnectPolicy)this.connectPolicy;

            var masterServer = (ITestMasterApplication)connPolicy.MasterServer.Application;
            masterServer.ResetStats();

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);
                masterClient1 = this.CreateGameOnGameServer(this.Player1, roomName, null, 0, true, true, 3, null, null, checkUserOnJoin:true);

                const int waitDelta = 3_000;
                // we initiate connection break and wait untill GSs are moreved from master + waitDelta
                connPolicy.MasterServer.SimulateConnectionBreak((uint)MasterServerSettings.Default.GSContextTTL + waitDelta);

                // wait till game server context will be removed
                Thread.Sleep(MasterServerSettings.Default.GSContextTTL + waitDelta + 1000);

                // join 2nd client on master: 
                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);

                // gs is not reconnected yet, so we will not find our player in cache
                masterClient2.FindFriends(new string[] { this.Player1 }, out var onlineStates, out var userStates);

                Assert.That(onlineStates[0], Is.False);

                Thread.Sleep(GameServerSettings.Default.ConnectReytryInterval * 1000);

                // now GS is reconnected and we should be able to find our player online
                masterClient2.FindFriends(new string[] { this.Player1 }, out onlineStates, out userStates);

                Assert.That(onlineStates[0], Is.True);
                Assert.That(userStates[0], Is.EqualTo(roomName));
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
                connPolicy.GameServer.Stop();
                connPolicy.GameServer.Start();
                masterServer.ResetStats();
            }
        }

        [Test]
        public void TestLobbyCountAfterConnectionBreak()
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }

            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("this tests requires userId to be set");
            }

            Thread.Sleep(1000);// to finish everything from perv tests

            var connPolicy = (OfflineConnectPolicy)this.connectPolicy;

            var masterServer = (ITestMasterApplication)connPolicy.MasterServer.Application;
            masterServer.ResetStats();

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            try
            {
                string roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    PlayerTTL = 300000,
                    CheckUserOnJoin = true,
                };
                var createGameResponse = masterClient1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(masterClient1, createGameResponse.Address);

                createGameRequest.AddUsers = new string[] {"Player3", "Player5", "Player6", "Player7"};

                masterClient1.CreateGame(createGameRequest);
                Thread.Sleep(300);

                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var response = masterClient2.JoinGame(roomName, ErrorCode.Ok);
                masterClient3.JoinGame(roomName, ErrorCode.Ok);

                this.ConnectAndAuthenticate(masterClient3, response.Address);

                masterClient3.JoinGame(roomName, ErrorCode.Ok);

                Thread.Sleep(300);

                masterClient3.Disconnect();

                connPolicy.MasterServer.SimulateConnectionBreak(1000);

                Thread.Sleep(GameServerSettings.Default.ConnectReytryInterval * 1000 + 1000);

                this.ConnectAndAuthenticate(masterClient3, response.Address);
                var joinRequest = new JoinGameRequest
                {
                    GameId = roomName,
                    JoinMode = (byte)JoinMode.JoinOrRejoin
                };
                masterClient3.JoinGame(joinRequest, ErrorCode.Ok);

                Thread.Sleep(300);

                masterClient3.Disconnect();

                Thread.Sleep(4000);

                masterClient1.Disconnect();

                Thread.Sleep(1000);
                this.ConnectAndAuthenticate(masterClient2, this.MasterAddress);
                var getLobbyStatsResponse = masterClient2.GetLobbyStats(null, null);

                masterClient2.Disconnect();

                Assert.That(getLobbyStatsResponse.PeerCount[0], Is.EqualTo(0));
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }

        [Test]
        public void TestLobbyCountAfterRemovingInactivePlayerBreak()
        {
            if (this.connectPolicy.IsOnline)
            {
                Assert.Ignore("This is an offline test");
            }

            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("this tests requires userId to be set");
            }

            Thread.Sleep(1000);// to finish everything from perv tests

            var connPolicy = (OfflineConnectPolicy)this.connectPolicy;

            var masterServer = (ITestMasterApplication)connPolicy.MasterServer.Application;
            masterServer.ResetStats();

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            try
            {
                string roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                masterClient1 = this.CreateMasterClientAndAuthenticate(this.Player1);
                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    PlayerTTL = 3000,
                    CheckUserOnJoin = !string.IsNullOrEmpty(this.Player1),
                };
                var createGameResponse = masterClient1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(masterClient1, createGameResponse.Address);

                //createGameRequest.AddUsers = new string[] { "Player3", "Player5", "Player6", "Player7" };

                masterClient1.CreateGame(createGameRequest);
                Thread.Sleep(300);

                masterClient2 = this.CreateMasterClientAndAuthenticate(this.Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(this.Player3);

                var response = masterClient2.JoinGame(roomName, ErrorCode.Ok);
                masterClient3.JoinGame(roomName, ErrorCode.Ok);

                this.ConnectAndAuthenticate(masterClient3, response.Address);

                masterClient3.JoinGame(roomName, ErrorCode.Ok);

                Thread.Sleep(300);

                masterClient3.Disconnect();

                Thread.Sleep(4000);

                masterClient1.Disconnect();

                Thread.Sleep(1000);
                this.ConnectAndAuthenticate(masterClient2, this.MasterAddress);
                var getLobbyStatsResponse = masterClient2.GetLobbyStats(null, null);

                masterClient2.Disconnect();

                Assert.That(getLobbyStatsResponse.PeerCount[0], Is.EqualTo(0));
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3);
            }
        }

        #endregion

    }
}
