using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Photon.Common.Tools;
using Photon.SocketServer;
using Photon.LoadBalancing.ServerToServer.Operations;
using Photon.LoadBalancing.MasterServer;
using OperationCode = Photon.LoadBalancing.ServerToServer.Operations.OperationCode;

namespace Photon.LoadBalancing.Handler
{
    public abstract class BaseHandler
    {
        public OperationCode OpCode;

        public abstract void OnOperationRequest(OperationRequest operationRequest, SendParameters sendParameters, RedirectedClientPeer peer);
    }
}
