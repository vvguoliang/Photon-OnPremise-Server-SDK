namespace LoadTest
{
    using ExitGames.Logging;
    using Photon.NameServer.Operations;
    using Photon.SocketServer;
    using Photon.SocketServer.ServerToServer;
    using PhotonHostRuntimeInterfaces;

    class ClientPeer : OutboundS2SPeer
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly Client client;

        public ClientPeer(ApplicationBase application, Client client)
            : base(application)
        {
            this.client = client;
        }

        protected override void OnOperationRequest(OperationRequest operationRequest, SendParameters sendParameters)
        {
            throw new System.NotImplementedException();
        }

        protected override void OnDisconnect(DisconnectReason reasonCode, string reasonDetail)
        {
            this.client.OnDisconnect(reasonCode, reasonDetail);
        }

        protected override void OnEvent(IEventData eventData, SendParameters sendParameters)
        {
            throw new System.NotImplementedException();
        }

        protected override void OnOperationResponse(OperationResponse operationResponse, SendParameters sendParameters)
        {
            if (operationResponse.OperationCode == (int)OperationCode.Authenticate)
            {
                this.client.OnAuthResponse(operationResponse);
            }
            else
            {
                log.ErrorFormat("Unexpected response: {0}", operationResponse.OperationCode);
            }
        }

        protected override void OnConnectionEstablished(object responseObject)
        {
            this.client.OnConnectionEstablished();
        }

        protected override void OnConnectionFailed(int errorCode, string errorMessage)
        {
            this.client.OnConnectionFailed(errorCode, errorMessage);
        }

        protected override void OnInitializeEcryptionCompleted(short resultCode, string debugMessage)
        {
            base.OnInitializeEcryptionCompleted(resultCode, debugMessage);
            this.client.OnInitializeEncryptionCompleted(this, resultCode, debugMessage);
        }
    }
}
