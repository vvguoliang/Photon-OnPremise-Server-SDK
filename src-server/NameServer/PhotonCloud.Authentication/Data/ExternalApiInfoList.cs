namespace PhotonCloud.Authentication.Data
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    public class ExternalApiInfoList
    {
        [DataMember(IsRequired = true)]
        public List<ExternalApiInfo> Entries { get; set; }

        [DataMember(IsRequired = true)]
        public int TotalRecordCount { get; set; }

        public static bool IsNullOrEmpty(ExternalApiInfoList instance)
        {
            return instance == null || instance.TotalRecordCount == 0;
        }
    }
}
