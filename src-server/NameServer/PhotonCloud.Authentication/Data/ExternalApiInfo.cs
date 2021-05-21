namespace PhotonCloud.Authentication.Data
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    public class ExternalApiInfo
    {
        [DataMember(IsRequired = true)]
        public ExternalApiType ApiType { get; set; }

        [DataMember(IsRequired = true)]
        public ExternalApiSubType ApiSubType { get; set; }
        
        [DataMember(IsRequired = true)]
        public string ApiName { get; set; }

        [DataMember(IsRequired = false)]
        public List<ExternalApiValue> ApiValues { get; set; }
    }
}
