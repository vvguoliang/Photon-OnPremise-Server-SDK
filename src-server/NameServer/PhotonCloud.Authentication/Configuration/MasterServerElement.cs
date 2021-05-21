namespace PhotonCloud.Authentication.Configuration
{
    using System.Configuration;

    public class MasterServerElement : ConfigurationElement
    {
        [ConfigurationProperty("InternalIpAddress", IsRequired = true)] 
        public string InternalIpAddress
        {
            get
            {
                return (string)this["InternalIpAddress"];
            }
        }
    }
}
