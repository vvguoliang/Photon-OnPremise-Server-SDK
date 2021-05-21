namespace Photon.LoadBalancing.GameServer
{
    #region directives

    using System;
    using System.Collections;
    using System.Diagnostics;
    using System.Net;

    using ExitGames.Logging;

    using Photon.Common.LoadBalancer.LoadShedding.Diagnostics;
    using Photon.Hive.Operations;
    using Photon.LoadBalancing.Operations;
    using Photon.SocketServer;
    using Photon.SocketServer.ServerToServer;

    using OperationCode = Operations.OperationCode;

    #endregion

    public class TestClient
    {
        #region Fields / Constancts

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private TcpClient gameServerClient;

        private TestClientConnectionState connectionState = TestClientConnectionState.Initial;

        private IPEndPoint endPoint;

        private string userId;
        private string gameId;
        private string token;

        private int interval;

        protected static readonly Stopwatch watch = Stopwatch.StartNew();

        #endregion

        #region public methods

//        public void StartWithMaster(string address, string gameName, string token)
//        {
//            var master = new TestMasterClient();
//            master.Start("selfMonitoring", 0, token, this);
//        }

        public void Start(string ip, int port, string userId, string gameName, string token, int interval)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("TestClient({2}): Connecting to game server at {0}:{1}", ip, port, userId);
            }

            var ipaddress = IPAddress.Parse(ip);
            this.userId = userId;
            this.gameId = gameName;
            this.token = token;

            this.interval = interval;

            endPoint = new IPEndPoint(ipaddress, port);

            this.gameServerClient = new TcpClient();
            this.gameServerClient.ConnectError += this.OnGameClientConnectError;
            this.gameServerClient.ConnectCompleted += this.OnGameClientConnectCompleted;
            this.gameServerClient.OperationResponse += this.OnGameClientOperationResponse;
            this.gameServerClient.Event += OnGameClientEvent;
            this.gameServerClient.Disconnected += OnGameClientDisconnected;

            this.connectionState = TestClientConnectionState.Connecting;

            this.gameServerClient.Connect(endPoint, "Game");
        }

        public TestClientConnectionState GetConnectionState()
        {
            return connectionState;
        }

        #endregion

        private void Authenticate()
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("TestClient, Authenticate {0}", userId);
            }

            var operation = new AuthenticateRequest { Token = this.token };
            var request = new OperationRequest((byte)OperationCode.Authenticate, operation);
            this.gameServerClient.SendOperationRequest(request, new SendParameters());
        }

        private void JoinOrCreateGameOnGameServer(bool rejoin = false)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("TestClient({1}): JoinCreate game {0}", gameId, userId);
            }

            //invisible game is not necessary, this is an separate monitoring app
            var gameProperties = new Hashtable { { (byte)GameParameter.IsVisible, false},  };
            var operation = new JoinGameRequest { GameId = this.gameId, GameProperties = gameProperties};
            var request = new OperationRequest((byte)OperationCode.JoinGame, operation);
            //setting join mode in JoinGameRequest does nothing (1=CreateIfNotExist, 3=RejoinOnly)
            request.Parameters[215] = rejoin ? 3 : 1;   //
            this.gameServerClient.SendOperationRequest(request, new SendParameters());
        }

        private void OnGameClientConnectCompleted(object sender, EventArgs e)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("TestClient({0}): Successfully connected to game server.", userId);
            }

            this.connectionState = TestClientConnectionState.Connected;

            this.Authenticate();
        }

        private void OnGameClientConnectError(object sender, SocketErrorEventArgs e)
        {
            log.WarnFormat("TestClient({1}): Failed to connect to game server: error = {0}", e.SocketError, userId);

            //TODO connection failed state? or set an error?
            this.connectionState = TestClientConnectionState.Disconnected;
        }



        private void OnGameClientDisconnected(object sender, SocketErrorEventArgs e)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("TestClient({0}): disconnected from game server.", userId);
            }

            if (this.connectionState != TestClientConnectionState.Stopped)
            {
                this.connectionState = TestClientConnectionState.Disconnected;
            }
        }

        private void OnGameClientEvent(object sender, EventDataEventArgs e)
        {
            switch (e.EventData.Code)
            {
                case 100:
                {
                    var data = (Hashtable)e.EventData[245];
                    long now = watch.ElapsedMilliseconds;
                    var sendTime = (long)data[0];
                    long diff = now - sendTime;
//                    if (log.IsDebugEnabled)
//                    {
//                        log.DebugFormat("RTT: {0}ms", diff);
//                    }
                    Counter.SelfMonitoringRtt.IncrementBy(diff);
                    break;
                }

                case (byte)EventCode.Join:
                case (byte)EventCode.Leave:
                    //do nothing
                    break;

                default:
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("TestClient({1}): received unhandled event '{0}'", e.EventData.Code, userId);
                    }
                    break;
            }
        }

        private void OnGameClientOperationResponse(object sender, OperationResponseEventArgs e)
        {
            if (e.OperationResponse.ReturnCode != 0)
            {
                log.WarnFormat(
                    "TestClient({3}): Received error response: code={0}, result={1}, msg={2}",
                    e.OperationResponse.OperationCode,
                    e.OperationResponse.ReturnCode,
                    e.OperationResponse.DebugMessage,
                    userId);
                return;
            }

            switch (e.OperationResponse.OperationCode)
            {
                case (byte)OperationCode.Authenticate:
                {
                    this.connectionState = TestClientConnectionState.Authenticated;

                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("TestClient({0}): Successfully authenticated", userId);
                    }

                    JoinOrCreateGameOnGameServer();
                    break;
                }

                case (byte)Operations.OperationCode.JoinGame:
                    {

                        this.connectionState = TestClientConnectionState.InGame;

                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("TestClient({0}): Successfully joined/created game '{1}'", userId, gameId);
                        }
                        break;
                    }

                default:
                    {
                        log.WarnFormat("TestClient({1}): received response for unexpected operation: {0}", e.OperationResponse.OperationCode, userId);
                        return;
                    }
            }
        }

        public void Stop()
        {
            connectionState = TestClientConnectionState.Stopped;

            if (this.gameServerClient != null && this.gameServerClient.Connected)
            {
                this.gameServerClient.Disconnect();
            }
        }

        private DateTime lastEvent = DateTime.MinValue;

        public void Service()
        {
            if (connectionState == TestClientConnectionState.Disconnected)
            {
                Rejoin();
                return;
            }

            if (connectionState != TestClientConnectionState.InGame)
            {
                return;
            }

            if ((DateTime.UtcNow - lastEvent).Milliseconds < interval)
            {
                return;
            }

            lastEvent = DateTime.UtcNow;

            var data = new Hashtable { {0, watch.ElapsedMilliseconds } };

            var operation = new RaiseEventRequest { EvCode = 100, Data = data, ReceiverGroup = 1};
            var request = new OperationRequest((byte)Photon.Hive.Operations.OperationCode.RaiseEvent, operation);

            this.gameServerClient.SendOperationRequest(request, new SendParameters());

//            if (log.IsDebugEnabled)
//            {
//                log.DebugFormat("TestClient({0}): sent event", userId);
//            }
        }

        private void Rejoin()
        {
            if (this.connectionState == TestClientConnectionState.Stopped)
            {
                return;
            }   

            this.connectionState = TestClientConnectionState.Connecting;
            gameServerClient.Connect(endPoint, "Game");

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("TestClient({0}): trying to rejoin game '{1}'", userId, gameId);
            }
        }

    }

    public enum TestClientConnectionState
    {
        Initial,
        Connecting,
        Connected,
        Authenticated,
        InGame,
        Disconnected,
        Stopped,
    }
}