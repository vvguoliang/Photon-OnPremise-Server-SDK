using Photon.Common.Authentication;

namespace Photon.NameServer.Operations
{
    using Photon.SocketServer;
    using Photon.SocketServer.Rpc;

    public class AuthenticateRequest : Operation, IAuthenticateRequest
    {
        #region Constructors and Destructors

        public AuthenticateRequest(IRpcProtocol protocol, OperationRequest operationRequest)
            : base(protocol, operationRequest)
        {
        }

        public AuthenticateRequest()
        {
        }

        #endregion

        #region Properties

        [DataMember(Code = (byte)ParameterKey.ApplicationId, IsOptional = true)]
        public string ApplicationId { get; set; }

        [DataMember(Code = (byte)ParameterKey.AppVersion, IsOptional = true)]
        public string ApplicationVersion { get; set; }

        [DataMember(Code = (byte)ParameterKey.Token, IsOptional = true)]
        public string Token { get; set; }

        [DataMember(Code = (byte)ParameterKey.UserId, IsOptional = true)]
        public string UserId { get; set; }

        [DataMember(Code = (byte)ParameterKey.ClientAuthenticationType, IsOptional = true)]
        public byte ClientAuthenticationType { get; set; }

        [DataMember(Code = (byte)ParameterKey.ClientAuthenticationParams, IsOptional = true)]
        public string ClientAuthenticationParams { get; set; }

        [DataMember(Code = (byte)ParameterKey.ClientAuthenticationData, IsOptional = true)]
        public object ClientAuthenticationData { get; set; }

        [DataMember(Code = (byte)ParameterKey.Region, IsOptional = false)]
        public string Region { get; set; }

        [DataMember(Code = (byte)ParameterKey.Flags, IsOptional = true)]
        public int Flags { get; set; }

        #endregion
    }
}
