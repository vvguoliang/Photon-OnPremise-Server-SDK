namespace PhotonCloud.Authentication.Configuration
{
    using System.Configuration;
    using System.IO;
    using System.Xml;

    public class MasterServersConfiguration : ConfigurationSection
    {
        public void Open(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var xmlReader = new XmlTextReader(fs))
            {
                this.DeserializeElement(xmlReader, false);
            }
        }

        [ConfigurationProperty("Servers", IsRequired = false)]
        public MasterServerElementCollection Servers
        {
            get
            {
                return (MasterServerElementCollection)base["Servers"];
            }
        }
    }
}
