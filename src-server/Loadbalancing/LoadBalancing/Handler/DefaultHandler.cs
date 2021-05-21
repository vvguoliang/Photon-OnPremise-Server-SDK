using Photon.SocketServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Photon.LoadBalancing.MasterServer;

namespace Photon.LoadBalancing.Handler
{
    public class DefaultHandler : BaseHandler
    {
        public DefaultHandler()
        {
            OpCode = ServerToServer.Operations.OperationCode.Default;
        }


        public override void OnOperationRequest(OperationRequest operationRequest, SendParameters sendParameters, RedirectedClientPeer peer)
        {
            throw new NotImplementedException();
        }

    }
}
