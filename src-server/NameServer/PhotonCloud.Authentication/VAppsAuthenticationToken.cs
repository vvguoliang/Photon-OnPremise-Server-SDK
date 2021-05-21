
using Photon.Common.Authentication;

namespace PhotonCloud.Authentication
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using Photon.SocketServer;

    public class VAppsAuthenticationToken : AuthenticationToken
    {
        public VAppsAuthenticationToken(IRpcProtocol protocol, IDictionary<byte, object> dataMembers)
            : base(protocol, dataMembers)
        {
        }

        private static readonly IRpcProtocol serializationProtocol = Protocol.GpBinaryV17;

        // we are using the Version as "EventCode". 
        public VAppsAuthenticationToken()
        {
        }

        [Photon.SocketServer.Rpc.DataMember(Code = 5, IsOptional = true)]
        public int MaxCcu { get; set; }

        [Photon.SocketServer.Rpc.DataMember(Code = 6, IsOptional = true)]
        public bool IsCcuBurstAllowed { get; set; }

        [Photon.SocketServer.Rpc.DataMember(Code = 7, IsOptional = true)]
        public string PrivateCloud { get; set; }

        [Photon.SocketServer.Rpc.DataMember(Code = 9, IsOptional = true)]
        public bool HasExternalApi { get; set; }

        public override bool AreEqual(AuthenticationToken rhs)
        {
            if (!base.AreEqual(rhs))
            {
                return false;
            }

            var rhs2 = rhs as VAppsAuthenticationToken;
            if (rhs2 == null)
            {
                return false;
            }

            return this.ApplicationId == rhs2.ApplicationId
                   && this.ApplicationVersion == rhs2.ApplicationVersion;
        }

        public override byte[] Serialize()
        {
            return serializationProtocol.SerializeEventData(new EventData(Version, this));
        }

        public new static bool TryDeserialize(byte[] data, out AuthenticationToken token, out string errorMsg)
        {
            token = null;
            EventData eventData;
            if (!serializationProtocol.TryParseEventData(data, out eventData, out errorMsg))
            {
                return false;
            }

            // code = version
            switch (eventData.Code)
            {
                default:
                    errorMsg = string.Format("Unknown version of token: {0}", eventData.Code);
                    return false;

                case 1:
                    token = new VAppsAuthenticationToken(serializationProtocol, eventData.Parameters);
                    return true;
            }
        }
    }
}
