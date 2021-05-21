// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NodeInfoComparer.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the NodeInfoComparer type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace PhotonCloud.NameServer
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Compares node entries from the Nameserver.json by Cloud / Region / Cluster. 
    /// </summary>
    public class PhotonEndpointInfoComparer : IEqualityComparer<CloudPhotonEndpointInfo>
    {
        public bool Equals(CloudPhotonEndpointInfo x, CloudPhotonEndpointInfo y)
        {
            //Check whether the compared objects reference the same data.
            if (Object.ReferenceEquals(x, y)) return true;

            //Check whether any of the compared objects is null.
            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;

            // check if PrivateCloud, Region and Cluster are equal. 
            return x.PrivateCloud.Equals(y.PrivateCloud, StringComparison.InvariantCulture)
                    && x.Region.Equals(y.Region, StringComparison.InvariantCulture)
                    && x.Cluster.Equals(y.Cluster, StringComparison.InvariantCulture); 
        }

        public int GetHashCode(CloudPhotonEndpointInfo photonEndpointInfo)
        {
            // Check whether the object is null
            if (ReferenceEquals(photonEndpointInfo, null))
            {
                return 0;
            }

            // Get hash code for the PrivateCloud field if it is not null.
            int hashPrivateCloud = photonEndpointInfo.PrivateCloud == null ? 0 : photonEndpointInfo.PrivateCloud.GetHashCode();

            // Get hash code for the Region field if it is not null.
            int hashRegion = photonEndpointInfo.Region == null ? 0 : photonEndpointInfo.Region.GetHashCode();

            // Get hash code for the Cluster field if it is not null.
            int hashCluster = photonEndpointInfo.Cluster == null ? 0 : photonEndpointInfo.Cluster.GetHashCode(); 

            // Calculate the hash code for the product.
            return hashPrivateCloud ^ hashRegion ^ hashCluster; 
        }
    }
}
