namespace PhotonCloud.Authentication.Data
{
    using System.Runtime.Serialization;

    public class ExternalApiValue
    {
        [DataMember(IsRequired = true)]
        public string Name { get; set; }

        [DataMember(IsRequired = true)]
        public string Value { get; set; }

        [DataMember(IsRequired = true)]
        public bool Mandatory { get; set; }

        [DataMember(IsRequired = true)]
        public bool ReadOnlyValue { get; set; }
    }
}
