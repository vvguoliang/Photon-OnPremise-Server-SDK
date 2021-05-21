namespace PhotonCloud.Authentication.Data
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    public class ClientAuthenticationServiceInfoList
    {
        [DataMember(IsRequired = true)]
        public List<ClientAuthenticationServiceInfo> Entries { get; set; }

        [DataMember(IsRequired = true)]
        public int TotalRecordCount { get; set; }
    }
}
