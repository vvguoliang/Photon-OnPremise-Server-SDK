
namespace PhotonCloud.NameServer.Operations
{
    using System.Collections.Generic;
    using Photon.NameServer.Operations;
    using Photon.SocketServer;
    using Photon.SocketServer.Rpc;
    public class AuthenticateResponse : Photon.NameServer.Operations.AuthenticateResponse
    {
        public AuthenticateResponse()
        {
        }

        public AuthenticateResponse(IRpcProtocol protocol, Dictionary<byte, object> parameter)
            : base(protocol, parameter)
        {
        }
        [DataMember(Code = (byte)ParameterKey.Cluster, IsOptional = true)]
        public string Cluster { get; set; }
    }
}
