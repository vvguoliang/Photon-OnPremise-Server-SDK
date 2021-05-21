
namespace PhotonCloud.NameServer
{
    using PhotonCloud.NameServer.Configuration;
    using Photon.NameServer;
    using System.Collections.Generic;
    using Photon.Common.Authentication.Data;

    public class CloudPhotonEndpointInfo : PhotonEndpointInfo
    {
        public CloudPhotonEndpointInfo(Node nodeInfo) : base (nodeInfo)
        {
            if (nodeInfo.Cluster == null)
            {
                nodeInfo.Cluster = "default"; 
            }

            if (string.IsNullOrEmpty(nodeInfo.PrivateCloud))
            {
                nodeInfo.PrivateCloud = "public";
            }

            // internal use: 
            this.ServiceType = nodeInfo.ServiceTypes;

            this.PrivateCloud = nodeInfo.PrivateCloud.ToLower();
            this.Cluster = nodeInfo.Cluster.ToLower();
            this.UseV1Token = nodeInfo.UseV1Token;
        }

        // for internal use: 
        public List<ServiceType> ServiceType { get; private set; }

        public string PrivateCloud { get; private set; }

        public string Cluster { get; private set; }
        
        public bool UseV1Token { get; private set; }

        public override string ToString()
        {
            return string.Format(
                "MasterServerConfig - ServiceType: {0}, PrivateCloud: {1}, Region: {2}, Cluster: {3}, UseV1Token: {4}",
                this.ServiceType,
                this.PrivateCloud,
                this.Region,
                this.Cluster,
                this.UseV1Token
                );
        }
    }
}
