using System;
using System.Collections.Generic;
using System.Text;

namespace PhotonCloud.NameServer.Configuration
{
    [Serializable()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "", IsNullable = false, ElementName = "ServerConfig")]
    public class CloudServerConfig : Photon.NameServer.Configuration.ServerConfig
    {
        public string CloudType;
    }
}
