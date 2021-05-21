using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using ExitGames.Client.Photon;
using Photon.Realtime;
using NUnit.Framework;
using Photon.Hive.Operations;
using Photon.Hive.Plugin;
using Photon.LoadBalancing.Operations;
using Photon.LoadBalancing.UnifiedClient;
using Photon.UnitTest.Utils.Basic;
using ErrorCode = Photon.Realtime.ErrorCode;
using EventCode = Photon.Realtime.EventCode;
using OperationCode = Photon.Realtime.OperationCode;
using ParameterCode = Photon.Realtime.ParameterCode;

namespace Photon.LoadBalancing.UnitTests.UnifiedTests
{
    public abstract partial class LBApiTestsImpl
    {
        #region RoomOptions Tests

        [Test]
        public void RoomFlags_BroadcastTest()
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                string roomName = this.GenerateRandomizedRoomName("MaxPlayers_");
                client1 = this.CreateMasterClientAndAuthenticate(this.Player1);

                var cr = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = new Hashtable { { "key", "value" } },
                    RoomFlags = RoomOptionFlags.BroadcastPropsChangeToAll
                };

                var response = client1.CreateGame(cr);

                this.ConnectAndAuthenticate(client1, response.Address);

                response = client1.CreateGame(cr);

                // join 2nd client : 
                client2 = this.CreateMasterClientAndAuthenticate(Player2);
                var jr = client2.JoinGame(roomName);

                this.ConnectAndAuthenticate(client2, jr.Address);

                var jr2 = client2.JoinGame(roomName);

                Assert.That(jr2.RoomFlags, Is.EqualTo(RoomOptionFlags.BroadcastPropsChangeToAll));
            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }


        /// <summary>
        /// we use test case "Conflict" to check how it works in case if both bool flag and bit field is set
        /// later should win
        /// </summary>
        /// <param name="testCase"></param>
        [Test]
        public void RoomFlags_PublishUserIdTest([Values("UseFlag", "UseFlags", "Conflict")] string testCase)
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");

                var joinRequest = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, roomName},
                        {ParameterCode.JoinMode, JoinModes.CreateIfNotExists},
                        {ParameterCode.CheckUserOnJoin, !string.IsNullOrEmpty(this.Player1)},
                        { ParameterCode.Broadcast, true},
                    }
                };

                if (testCase == "UseFlags")
                {
                    joinRequest.Parameters.Add((byte)ParameterKey.RoomOptionFlags, RoomOptionFlags.PublishUserId);
                }
                else if (testCase == "UseFlag")
                {
                    joinRequest.Parameters.Add(ParameterCode.PublishUserId, true);
                }
                else
                {
                    joinRequest.Parameters.Add((byte)ParameterKey.RoomOptionFlags, RoomOptionFlags.PublishUserId);
                    joinRequest.Parameters.Add(ParameterCode.PublishUserId, false);
                }

                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(Player2);

                if (string.IsNullOrEmpty(this.Player1) && masterClient1.Token == null)
                {
                    Assert.Ignore("This test does not work correctly for old clients without userId and token");
                }

                var joinResponse = masterClient1.SendRequestAndWaitForResponse(joinRequest);
                var address = (string) joinResponse[ParameterCode.Address];

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, address);
                masterClient1.SendRequestAndWaitForResponse(joinRequest);

                //var actorProperties = (Hashtable)joinResponse[ParameterCode.PlayerProperties];
                var joinEvent = masterClient1.WaitForEvent(EventCode.Join);

                var actorProperties = (Hashtable) joinEvent[ParameterCode.PlayerProperties];
                var userId = (string) actorProperties[(byte) ActorParameter.UserId];
                if (string.IsNullOrEmpty(this.Player1))
                {
                    Assert.IsFalse(string.IsNullOrEmpty(userId));
                }
                else
                {
                    Assert.AreEqual(this.Player1, userId);
                }

                joinResponse = masterClient2.SendRequestAndWaitForResponse(joinRequest);
                address = (string) joinResponse[ParameterCode.Address];

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient2, address);

                masterClient2.OperationResponseQueueClear();
                joinResponse = masterClient2.SendRequestAndWaitForResponse(joinRequest);

                actorProperties = (Hashtable) joinResponse[ParameterCode.PlayerProperties];
                var actor0Properties = (Hashtable) actorProperties[1];
                userId = (string) actor0Properties[(byte) ActorParameter.UserId];
                if (string.IsNullOrEmpty(this.Player1))
                {
                    Assert.IsFalse(string.IsNullOrEmpty(userId));
                }
                else
                {
                    Assert.AreEqual(this.Player1, userId);
                }

                joinEvent = masterClient1.WaitForEvent(EventCode.Join);

                actorProperties = (Hashtable) joinEvent[ParameterCode.PlayerProperties];
                userId = (string) actorProperties[(byte) ActorParameter.UserId];

                if (string.IsNullOrEmpty(this.Player2))
                {
                    Assert.IsFalse(string.IsNullOrEmpty(userId));
                }
                else
                {
                    Assert.AreEqual(this.Player2, userId);
                }
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        [Test]
        public void RoomFlags_NotPublishUserIdTest()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;
            UnifiedTestClient masterClient3 = null;
            UnifiedTestClient masterClient4 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");
                var joinRequest = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, roomName},
                        {ParameterCode.JoinMode, JoinModes.CreateIfNotExists},
                        {ParameterCode.CheckUserOnJoin, !string.IsNullOrEmpty(this.Player1)},
                        {ParameterCode.Broadcast, true},
                    }
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(Player2);
                masterClient3 = this.CreateMasterClientAndAuthenticate(Player3);
                masterClient4 = this.CreateMasterClientAndAuthenticate(Player3 != null ? "Player4" : null);

                var joinResponse = masterClient1.SendRequestAndWaitForResponse(joinRequest);
                var address = (string) joinResponse[ParameterCode.Address];

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, address);
                masterClient1.SendRequestAndWaitForResponse(joinRequest);

                //var actorProperties = (Hashtable)joinResponse[ParameterCode.PlayerProperties];
                var joinEvent = masterClient1.WaitForEvent(EventCode.Join);

                var actorProperties = (Hashtable) joinEvent[ParameterCode.PlayerProperties];
                Assert.IsNull(actorProperties);

                joinResponse = masterClient2.SendRequestAndWaitForResponse(joinRequest);
                address = (string) joinResponse[ParameterCode.Address];

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient2, address);
                joinResponse = masterClient2.SendRequestAndWaitForResponse(joinRequest);

                actorProperties = (Hashtable) joinResponse[ParameterCode.PlayerProperties];
                Assert.IsNull(actorProperties);

                joinEvent = masterClient1.WaitForEvent(EventCode.Join);

                actorProperties = (Hashtable) joinEvent[ParameterCode.PlayerProperties];
                Assert.IsNull(actorProperties);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2, masterClient3, masterClient4);
            }
        }

        [Test]
        public void RoomFlags_SuppressRoomEvents([Values("UseFlag", "UseFlags", "Conflict")] string testCase)
        {
            UnifiedTestClient client1 = null;
            UnifiedTestClient client2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName("SuppressRoomEvents_");

                client1 = this.CreateMasterClientAndAuthenticate(Player1);
                client2 = this.CreateMasterClientAndAuthenticate(Player2);

                var createGameResponse = client1.CreateGame(roomName, true, true, 4);

                // switch client 1 to GS 
                this.ConnectAndAuthenticate(client1, createGameResponse.Address, client1.UserId);

                var createRequest = new OperationRequest { OperationCode = OperationCode.CreateGame, Parameters = new Dictionary<byte, object>() };
                createRequest.Parameters.Add(ParameterCode.RoomName, createGameResponse.GameId);

                if (testCase == "UseFlags")
                {
                    createRequest.Parameters.Add((byte)ParameterKey.RoomOptionFlags, RoomOptionFlags.SuppressRoomEvents);
                }
                else if (testCase == "UseFlag")
                {
                    createRequest.Parameters.Add((byte)Operations.ParameterCode.SuppressRoomEvents, true);
                }
                else
                {
                    createRequest.Parameters.Add((byte)ParameterKey.RoomOptionFlags, RoomOptionFlags.SuppressRoomEvents);
                    createRequest.Parameters.Add((byte)Operations.ParameterCode.SuppressRoomEvents, false);
                }

                client1.SendRequestAndWaitForResponse(createRequest);

                this.ConnectAndAuthenticate(client2, createGameResponse.Address, client1.UserId);
                client2.JoinGame(roomName);

                EventData eventData;
                Assert.IsFalse(client1.TryWaitForEvent(EventCode.Join, ConnectPolicy.WaitTime, out eventData));

                client1.Dispose();
                Assert.IsFalse(client2.TryWaitForEvent(EventCode.Leave, ConnectPolicy.WaitTime, out eventData));

            }
            finally
            {
                DisposeClients(client1, client2);
            }
        }

        [Test]
        public void RoomFlags_BroadcastOnChange([Values("ToAll", "ToOthers")]string policy)
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                // create game on the game server
                string roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var gameProperties = new Hashtable();
                gameProperties["P1"] = 1;
                gameProperties["P2"] = 2;
                gameProperties["L1"] = 1;
                gameProperties["L2"] = 2;
                gameProperties["L3"] = 3;

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    GameProperties = gameProperties,
                    RoomFlags = policy == "ToAll" ? RoomOptionFlags.BroadcastPropsChangeToAll : 0,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);
                var cgResponse = masterClient1.CreateGame(createGameRequest);
                this.ConnectAndAuthenticate(masterClient1, cgResponse.Address);
                masterClient1.CreateGame(createGameRequest);

                masterClient2 = this.CreateMasterClientAndAuthenticate(Player2);

                var joinRequest = new JoinGameRequest()
                {
                    GameId = roomName,
                };
                var jgResponse = masterClient2.JoinGame(joinRequest, ErrorCode.Ok);
                this.ConnectAndAuthenticate(masterClient2, jgResponse.Address);
                masterClient2.JoinGame(joinRequest);


                masterClient1.EventQueueClear();
                masterClient1.SendRequest(new OperationRequest()
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        { (byte)ParameterKey.Properties, new Hashtable { {"P2", 22} } },
                        {(byte)ParameterKey.Broadcast, true }
                    }
                });

                masterClient2.CheckThereIsEvent(EventCode.PropertiesChanged, this.WaitTimeout);
                if (policy == "ToOthers")
                {
                    masterClient1.CheckThereIsNoEvent(EventCode.PropertiesChanged, this.WaitTimeout);
                }
                else
                {
                    masterClient1.CheckThereIsEvent(EventCode.PropertiesChanged, this.WaitTimeout);
                }
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        [Test]
        public void RoomFlags_DeleteNullPropsTest()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                // create game on the game server
                string roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var gameProperties = new Hashtable();
                gameProperties["P1"] = 1;
                gameProperties["P2"] = 2;
                gameProperties["L1"] = 1;
                gameProperties["L2"] = 2;
                gameProperties["L3"] = 3;

                var createGameRequest = new OperationRequest
                {
                    OperationCode = OperationCode.CreateGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        { ParameterCode.RoomName, roomName },
                        { ParameterCode.Properties, gameProperties},
                    }
                };

                createGameRequest.Parameters.Add((byte)ParameterKey.RoomOptionFlags, RoomOptionFlags.DeleteNullProps);


                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);
                var cgResponse = masterClient1.SendRequestAndWaitForResponse(createGameRequest);

                this.ConnectAndAuthenticate(masterClient1, (string)cgResponse[ParameterCode.Address]);

                masterClient1.SendRequestAndWaitForResponse(createGameRequest);

                masterClient1.OpSetPropertiesOfRoom
                (
                    new Hashtable
                    {
                        {"P1", null}
                    }
                );

                masterClient2 = this.CreateMasterClientAndAuthenticate(Player2);

                var joinRequest = new JoinGameRequest()
                {
                    GameId = roomName,
                };

                var jgResponse = masterClient2.JoinGame(joinRequest, ErrorCode.Ok);

                this.ConnectAndAuthenticate(masterClient2, jgResponse.Address);

                jgResponse = masterClient2.JoinGame(joinRequest);

                Assert.That(jgResponse.GameProperties.Contains("P1"), Is.False);

                masterClient1.OpSetPropertiesOfRoom
                (
                    new Hashtable
                    {
                        {"P2", null}
                    }
                );

                var propertiesChangedEvent = masterClient2.WaitForEvent(EventCode.PropertiesChanged, this.WaitTimeout);

                var properties = (Hashtable)propertiesChangedEvent[(byte)ParameterKey.Properties];

                Assert.That(properties, Is.Not.Null);
                Assert.That(properties.Contains("P2"));
                Assert.That(properties["P2"], Is.Null);

            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        [Test]
        public void RoomFlags_DeleteNullPropsAfterStateRestoreTest()
        {
            if (!this.UsePlugins)
            {
                Assert.Ignore("This test needs plugins");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                // create game on the game server
                string roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var gameProperties = new Hashtable();
                gameProperties["P1"] = 1;
                gameProperties["P2"] = 2;
                gameProperties["L1"] = 1;
                gameProperties["L2"] = 2;
                gameProperties["L3"] = 3;

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    RoomFlags = RoomOptionFlags.DeleteNullProps,
                    GameProperties = gameProperties,
                    Plugins = new[] { "SaveLoadStateTestPlugin" },
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);
                var cgResponse = masterClient1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(masterClient1, cgResponse.Address);

                masterClient1.CreateGame(createGameRequest);
                Thread.Sleep(100);
                masterClient1.LeaveGame();// leave game, so that game will be persisted

                Thread.Sleep(100);

                this.ConnectAndAuthenticate(masterClient1, this.MasterAddress);

                var joinRequest = new JoinGameRequest()
                {
                    GameId = roomName,
                    JoinMode = JoinModes.RejoinOrJoin,
                    Plugins = new[] { "SaveLoadStateTestPlugin" },
                };

                var jgResponse = masterClient1.JoinGame(joinRequest, ErrorCode.Ok);
                this.ConnectAndAuthenticate(masterClient1, jgResponse.Address);
                jgResponse = masterClient1.JoinGame(joinRequest);

                masterClient1.OpSetPropertiesOfRoom
                (
                    new Hashtable
                    {
                        {"P1", null}
                    }
                );

                masterClient2 = this.CreateMasterClientAndAuthenticate(Player2);
                jgResponse = masterClient2.JoinGame(joinRequest, ErrorCode.Ok);
                this.ConnectAndAuthenticate(masterClient2, jgResponse.Address);

                jgResponse = masterClient2.JoinGame(joinRequest);

                Assert.That(jgResponse.GameProperties.Contains("P1"), Is.False);

                masterClient1.OpSetPropertiesOfRoom
                (
                    new Hashtable
                    {
                        {"P2", null}
                    }
                );

                var propertiesChangedEvent = masterClient2.WaitForEvent(EventCode.PropertiesChanged, this.WaitTimeout);

                var properties = (Hashtable)propertiesChangedEvent[(byte)ParameterKey.Properties];

                Assert.That(properties, Is.Not.Null);
                Assert.That(properties.Contains("P2"));
                Assert.That(properties["P2"], Is.Null);

            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        [Test]
        public void RoomFlags_DeleteNullPropsCASTest()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                // create game on the game server
                string roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var gameProperties = new Hashtable();
                gameProperties["P1"] = 1;
                gameProperties["P2"] = 2;
                gameProperties["L1"] = 1;
                gameProperties["L2"] = 2;
                gameProperties["L3"] = 3;

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    RoomFlags = RoomOptionFlags.DeleteNullProps,
                    GameProperties = gameProperties,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);
                var cgResponse = masterClient1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(masterClient1, cgResponse.Address);

                masterClient1.CreateGame(createGameRequest);

                masterClient1.SendRequest(new OperationRequest()
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        { (byte)ParameterKey.Properties, new Hashtable { {"P1", null} } },
                        { (byte)ParameterKey.ExpectedValues, new Hashtable { {"P1", 1} } },
                    }
                });

                masterClient2 = this.CreateMasterClientAndAuthenticate(Player2);

                var joinRequest = new JoinGameRequest()
                {
                    GameId = roomName,
                };

                var jgResponse = masterClient2.JoinGame(joinRequest, ErrorCode.Ok);

                this.ConnectAndAuthenticate(masterClient2, jgResponse.Address);

                jgResponse = masterClient2.JoinGame(joinRequest);

                Assert.That(jgResponse.GameProperties.Contains("P1"), Is.False);

                masterClient1.SendRequest(new OperationRequest()
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        { (byte)ParameterKey.Properties, new Hashtable { {"P2", null} } },
                        { (byte)ParameterKey.ExpectedValues, new Hashtable { {"P2", 2} } },
                    }
                });

                var propertiesChangedEvent = masterClient2.WaitForEvent(EventCode.PropertiesChanged, this.WaitTimeout);

                var properties = (Hashtable)propertiesChangedEvent[(byte)ParameterKey.Properties];

                Assert.That(properties, Is.Not.Null);
                Assert.That(properties.Contains("P2"));
                Assert.That(properties["P2"], Is.Null);

            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        [Test]
        public void RoomFlags_DeleteNullPropsWrongCASTest()
        {
            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                // create game on the game server
                string roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name);

                var gameProperties = new Hashtable();
                gameProperties["P1"] = 1;
                gameProperties["P2"] = 2;
                gameProperties["L1"] = 1;
                gameProperties["L2"] = 2;
                gameProperties["L3"] = 3;

                var createGameRequest = new CreateGameRequest
                {
                    GameId = roomName,
                    RoomFlags = RoomOptionFlags.DeleteNullProps,
                    GameProperties = gameProperties,
                };

                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);
                var cgResponse = masterClient1.CreateGame(createGameRequest);

                this.ConnectAndAuthenticate(masterClient1, cgResponse.Address);

                masterClient1.CreateGame(createGameRequest);

                masterClient1.SendRequest(new OperationRequest()
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        { (byte)ParameterKey.Properties, new Hashtable { {"P1", null} } },
                        { (byte)ParameterKey.ExpectedValues, new Hashtable { {"P1", 12} } },
                    }
                });

                masterClient2 = this.CreateMasterClientAndAuthenticate(Player2);

                var joinRequest = new JoinGameRequest()
                {
                    GameId = roomName,
                };

                var jgResponse = masterClient2.JoinGame(joinRequest, ErrorCode.Ok);

                this.ConnectAndAuthenticate(masterClient2, jgResponse.Address);

                jgResponse = masterClient2.JoinGame(joinRequest);

                Assert.That(jgResponse.GameProperties.Contains("P1"), Is.True);

                masterClient1.SendRequest(new OperationRequest()
                {
                    OperationCode = OperationCode.SetProperties,
                    Parameters = new Dictionary<byte, object>
                    {
                        { (byte)ParameterKey.Properties, new Hashtable { {"P2", null} } },
                        { (byte)ParameterKey.ExpectedValues, new Hashtable { {"P2", 23} } },
                    }
                });

                masterClient2.CheckThereIsNoEvent(EventCode.PropertiesChanged, this.WaitTimeout);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }

        [Test]
        public void RoomFlags_CheckUserOnJoin([Values("UseFlag", "UseFlags", "Conflict")] string testCase)
        {
            if (string.IsNullOrEmpty(this.Player1))
            {
                Assert.Ignore("Test does work only if PlayerId is set");
            }

            UnifiedTestClient masterClient1 = null;
            UnifiedTestClient masterClient2 = null;

            try
            {
                var roomName = this.GenerateRandomizedRoomName(MethodBase.GetCurrentMethod().Name + "_");

                var joinRequest = new OperationRequest
                {
                    OperationCode = OperationCode.JoinGame,
                    Parameters = new Dictionary<byte, object>
                    {
                        {ParameterCode.RoomName, roomName},
                        {ParameterCode.JoinMode, JoinModes.CreateIfNotExists},
                    }
                };

                if (testCase == "UseFlags")
                {
                    joinRequest.Parameters.Add((byte)ParameterKey.RoomOptionFlags, RoomOptionFlags.CheckUserOnJoin);
                }
                else if (testCase == "UseFlag")
                {
                    joinRequest.Parameters.Add(ParameterCode.CheckUserOnJoin, true);
                }
                else
                {
                    joinRequest.Parameters.Add((byte)ParameterKey.RoomOptionFlags, RoomOptionFlags.CheckUserOnJoin);
                    joinRequest.Parameters.Add(ParameterCode.CheckUserOnJoin, false);
                }

                masterClient1 = this.CreateMasterClientAndAuthenticate(Player1);
                masterClient2 = this.CreateMasterClientAndAuthenticate(Player1);


                var joinResponse = masterClient1.SendRequestAndWaitForResponse(joinRequest);
                var address = (string)joinResponse[ParameterCode.Address];

                // client 1: connect to GS and try to join not existing game on the game server (create if not exists)
                this.ConnectAndAuthenticate(masterClient1, address);
                masterClient1.SendRequestAndWaitForResponse(joinRequest);

                masterClient2.SendRequestAndWaitForResponse(joinRequest, ErrorCode.JoinFailedPeerAlreadyJoined);
            }
            finally
            {
                DisposeClients(masterClient1, masterClient2);
            }
        }
        #endregion
    }
}
