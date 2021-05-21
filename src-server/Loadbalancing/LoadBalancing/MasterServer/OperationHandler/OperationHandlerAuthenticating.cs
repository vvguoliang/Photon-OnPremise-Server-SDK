
using Photon.LoadBalancing.Common;

namespace Photon.LoadBalancing.Master.OperationHandler
{
    using ExitGames.Logging;
    using Photon.LoadBalancing.MasterServer;
    using Photon.LoadBalancing.Operations;
    using Photon.SocketServer;
    using System.Collections.Generic;
    using ErrorCode = Photon.Common.ErrorCode;

    public class OperationHandlerAuthenticating : OperationHandlerBase
    {
        public static readonly OperationHandlerAuthenticating Instance = new OperationHandlerAuthenticating();

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        protected override OperationResponse OnOperationRequest(PeerBase peer, OperationRequest operationRequest, SendParameters sendParameters)
        {
            Dictionary<byte, object> dict = operationRequest.Parameters;
            foreach (object value in dict.Values)
            {
                MasterApplication.log.Info("============OperationHandlerAuthenticating==========:" + value.ToString());
                MasterApplication.log.Info("====operationRequest.OperationCode===:" + operationRequest.OperationCode.ToString());
            }

            switch (operationRequest.OperationCode)
            {
                default:
                    return HandleUnknownOperationCode(operationRequest, log);

                case (byte)OperationCode.Authenticate:
                    return new OperationResponse(operationRequest.OperationCode)
                    {
                        ReturnCode = (short)ErrorCode.OperationDenied, 
                        DebugMessage = LBErrorMessages.Authenticating
                    };

                case (byte)OperationCode.CreateGame:
                case (byte)OperationCode.JoinGame:
                case (byte)OperationCode.JoinLobby:
                case (byte)OperationCode.JoinRandomGame:
                case (byte)OperationCode.LeaveLobby:
                case (byte)OperationCode.DebugGame:
                case (byte)OperationCode.FindFriends:
                case (byte)OperationCode.LobbyStats:
                case (byte)OperationCode.Settings:
                case (byte)OperationCode.GetGameList:
                    return new OperationResponse(operationRequest.OperationCode)
                    {
                        ReturnCode = (short)ErrorCode.OperationDenied, 
                        DebugMessage = LBErrorMessages.NotAuthorized,
                    };
            }
        }
    }
}
