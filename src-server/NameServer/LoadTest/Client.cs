namespace LoadTest
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;

    using ExitGames.Logging;

    using LoadTest.Diagnostics;
    using LoadTest.Properties;

    using Photon.SocketServer;
    using PhotonHostRuntimeInterfaces;
    using Photon.NameServer.Operations;

    enum ClientState
    {
        Disconnected,
        Connecting,
        EstablishingEncryption,
        Authenticating,
        Authenticated,
        AuthFailed,
        FirstRequestSent,
        FirstRequestResponded,
        SecondRequestSent,
        SecondRequestResponded = Authenticated,
    };

    class Client
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private ClientPeer peer;

        public ClientState State { get; private set; }

        public LinkedListNode<Client> Node { get; set; }

        private readonly IPEndPoint masterEndPoint;

        private readonly string clientUserId;

        private readonly Stopwatch watcher = Stopwatch.StartNew();

        private long startTime = 0;

        private bool secondMethodCallDone = false;

        private readonly ClientManager manager;

        private bool takeOutFromQueue = false;

        private long lastDisconnectTime = 0;
        private long lastMethod1ResponseTime = 0;

        public Client(Application application, IPEndPoint masterEndPoint, string userId, ClientManager manager)
        {
            this.masterEndPoint = masterEndPoint;
            this.clientUserId = userId;
            this.manager = manager;
            this.peer = new ClientPeer(application, this);
            this.State = ClientState.Disconnected;
        }

        public void Do(long delta)
        {
            if (this.State == ClientState.Disconnected)
            {
                if (this.Node != null)
                {
                    if (this.takeOutFromQueue)
                    {
                        this.takeOutFromQueue = false;
                        this.manager.TakeMeOut(this);
                        return;
                    }
                }

                var now = this.watcher.ElapsedMilliseconds;
                if (this.lastDisconnectTime == 0 || (now - this.lastDisconnectTime > Settings.Default.ConnectLatencyMS))
                {
                    this.Connect();
                }
            }
            else if (this.State == ClientState.FirstRequestResponded)
            {
                var now = this.watcher.ElapsedMilliseconds;
                if (now - this.lastMethod1ResponseTime > Settings.Default.SecondRequestLatencyMS)
                {
                    this.SendAuthRequest2();
                }
            }
        }

        private void Connect()
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Connecting to ns: c:{0}", this.clientUserId);
            }
            if (this.peer.ConnectionState == ConnectionState.Disconnected
                || this.peer.ConnectionState == ConnectionState.Disposed)
            {
                startTime = watcher.ElapsedMilliseconds;

                this.State = ClientState.Connecting;
                //this.peer.ConnectTcp(this.masterEndPoint, "NameServer");
                this.peer.ConnectToServerUdp(this.masterEndPoint, "NameServer", 254, 1000);
            }
        }

        public void Close()
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("client closed: c:{0}", this.clientUserId);
            }

            if (this.peer != null)
            {
                this.peer.Disconnect();
                this.peer = null;
            }
        }

        public void OnDisconnect(DisconnectReason reasonCode, string reasonDetail)
        {
            this.State = ClientState.Disconnected;
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("client disconnected: c:{0}", this.clientUserId);
            }

            this.secondMethodCallDone = false;
            this.lastDisconnectTime = this.watcher.ElapsedMilliseconds;
        }

        public void OnConnectionEstablished()
        {
            var elapsedMilliseconds = watcher.ElapsedMilliseconds;

            Counters.ConnectionTime.IncrementBy(elapsedMilliseconds - this.startTime);

            this.State = ClientState.EstablishingEncryption;

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("client connection established: c:{0}", this.clientUserId);
            }

            this.peer.InitializeEncryption();
        }

        private void SendAuthRequest1()
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("client is sending auth request: c:{0}", this.clientUserId);
            }

            startTime = watcher.ElapsedMilliseconds;
            var authValues = new AuthenticationValues();
            authValues.SetAuthParameters("method1", "");
            if (this.OpAuthenticate(Settings.Default.AppId,
                Settings.Default.AppVer,
                this.clientUserId,
                authValues, Settings.Default.AppRegion))
            {
                Counters.RequestsSent.Increment();
                this.State = ClientState.FirstRequestSent;
            }
            else
            {
                this.peer.Disconnect();
                log.WarnFormat("Failed to send auth req. c:{0}", this.clientUserId);
            }
        }

        private void SendAuthRequest2()
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("client is sending auth request: c:{0}", this.clientUserId);
            }

            startTime = watcher.ElapsedMilliseconds;
            var authValues = new AuthenticationValues();
            authValues.SetAuthParameters("yes", "yes");
            if (this.OpAuthenticate(Settings.Default.AppId,
                Settings.Default.AppVer,
                this.clientUserId,
                authValues, Settings.Default.AppRegion))
            {
                Counters.RequestsSent.Increment();
                this.State = ClientState.SecondRequestSent;
            }
            else
            {
                log.WarnFormat("Failed to send auth req. c:{0}", this.clientUserId);
                this.peer.Disconnect(); 
            }
        }

        public bool OpAuthenticate(string appId, string appVersion, string userId, AuthenticationValues authValues, string regionCode)
        {
            var opParameters = new Dictionary<byte, object>();

            opParameters[(byte)ParameterKey.AppVersion] = appVersion;
            opParameters[(byte)ParameterKey.ApplicationId] = appId;

            if (!string.IsNullOrEmpty(regionCode))
            {
                opParameters[(byte)ParameterKey.Region] = regionCode;
            }

            if (!string.IsNullOrEmpty(userId))
            {
                opParameters[(byte)ParameterKey.UserId] = userId;
            }

            if (authValues != null && authValues.AuthType != CustomAuthenticationType.None)
            {
                opParameters[(byte)ParameterKey.ClientAuthenticationType] = (byte)authValues.AuthType;

                if (!string.IsNullOrEmpty(authValues.AuthParameters))
                {
                    opParameters[(byte)ParameterKey.ClientAuthenticationParams] = authValues.AuthParameters;
                }
                if (authValues.AuthPostData != null)
                {
                    opParameters[(byte)ParameterKey.ClientAuthenticationData] = authValues.AuthPostData;
                }
            }

            var request = new OperationRequest((byte)OperationCode.Authenticate, opParameters);
            return (this.peer.SendOperationRequest(request, new SendParameters{Encrypted = Settings.Default.EncryptData})
                == SendResult.Ok);
        }

        public void OnConnectionFailed(int errorCode, string errorMessage)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("client connection failed: c:{0}", this.clientUserId);
            }


            this.State = ClientState.Disconnected;

            Counters.ConnectFailures.Increment();
        }

        public void OnAuthResponse(OperationResponse operationResponse)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("client is authenitcated: c:{0}", this.clientUserId);
            }

            var elapsedMilliseconds = watcher.ElapsedMilliseconds;
            Counters.RoundTripTime.IncrementBy(elapsedMilliseconds - this.startTime);

            Counters.RequestsReceived.Increment();
            if (operationResponse.ReturnCode == 0)
            {
                if (this.State == ClientState.FirstRequestSent)
                {
                    this.State = ClientState.FirstRequestResponded;
                    Counters.FirstMethodResponses.Increment();
                    this.lastMethod1ResponseTime = this.watcher.ElapsedMilliseconds;
                }
                else if (this.State == ClientState.SecondRequestSent)
                {
                    Counters.SuccessResponses.Increment();

                    this.State = ClientState.Authenticated;
                    this.takeOutFromQueue = true;
                    this.peer.Disconnect();
                }
            }
            else
            {
                Counters.FailedResponses.Increment();
                this.State = ClientState.AuthFailed;
                this.peer.Disconnect();
            }
        }

        public void OnInitializeEncryptionCompleted(ClientPeer clientPeer, short resultCode, string debugMessage)
        {
            if (resultCode != 0)
            {
                if (log.IsInfoEnabled)
                {
                    log.InfoFormat("Encryption initialization failed. code:{0}, msg: {1}", resultCode, debugMessage);
                }
                clientPeer.Disconnect();
                return;
            }

            log.Debug("Encryption initialized.");
            this.State = ClientState.Authenticating;

            this.SendAuthRequest1();

        }
    }
}
