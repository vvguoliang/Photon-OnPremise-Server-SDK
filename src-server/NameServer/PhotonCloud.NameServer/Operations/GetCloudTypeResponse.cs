using Photon.NameServer.Operations;
using Photon.SocketServer.Rpc;

namespace PhotonCloud.NameServer.Operations
{
    public class GetCloudTypeResponse 
    {
        [DataMember(Code = (byte)ParameterKey.CloudType, IsOptional = true)]
        public string CloudType { get; set; }
    }
}
