using Photon.SocketServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Photon.Common.Tools;
using Photon.LoadBalancing.Manger;
using Photon.LoadBalancing.ServerToServer.Operations;
using Photon.LoadBalancing.MasterServer;
using OperationCode = Photon.LoadBalancing.ServerToServer.Operations.OperationCode;

namespace Photon.LoadBalancing.Handler
{
    class LoginHandler : BaseHandler
    {
        public LoginHandler()
        {
            OpCode = OperationCode.Login;
        }

        public override void OnOperationRequest(OperationRequest operationRequest, SendParameters sendParameters, RedirectedClientPeer peer)
        {
            string username = DictTools.GetValue<byte, object>(operationRequest.Parameters, (byte)ParameterCode.Username) as string;
            string password = DictTools.GetValue<byte, object>(operationRequest.Parameters, (byte)ParameterCode.Password) as string;

            UserManager manager = new UserManager();
            bool isSuccess = manager.VerifyUser(username, password);

            OperationResponse response = new OperationResponse(operationRequest.OperationCode);
            if (isSuccess)
            {
                response.ReturnCode = (short)ReturnCode.Success;
                peer.username = username;
                peer.password = password;
            }
            else
            {
                response.ReturnCode = (short)ReturnCode.Failed;
            }
            peer.SendOperationResponse(response, sendParameters);
        }
    }
}
