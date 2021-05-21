using Photon.SocketServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Photon.Common.Tools;
using Photon.LoadBalancing.Manger;
using Photon.LoadBalancing.Model;
using Photon.LoadBalancing.MasterServer;

namespace Photon.LoadBalancing.Handler
{
    class RegisterHandler : BaseHandler
    {
        public RegisterHandler()
        {
            OpCode = ServerToServer.Operations.OperationCode.Register;
        }


        public override void OnOperationRequest(OperationRequest operationRequest, SendParameters sendParameters, RedirectedClientPeer peer)
        {
            Dictionary<byte, object> dict = operationRequest.Parameters;
            foreach (object value in dict.Values)
            {
                MasterApplication.log.Info("============RegisterHandler==========:" + value.ToString());
            }

            string username = DictTools.GetValue<byte, object>(operationRequest.Parameters, (byte)ParameterCode.Username) as string;
            string password = DictTools.GetValue<byte, object>(operationRequest.Parameters, (byte)ParameterCode.Password) as string;

            UserManager manager = new UserManager();
            User user = manager.GetByUsername(username);

            OperationResponse response = new OperationResponse(operationRequest.OperationCode);
            if (user == null)
            {
                user = new User() { Username = username, Password = password };
                manager.Add(user);
                response.ReturnCode = (short)ReturnCode.Success;
            }
            else
            {
                response.ReturnCode = (short)ReturnCode.Failed;
            }
            peer.SendOperationResponse(response, sendParameters);
        }
    }
}
