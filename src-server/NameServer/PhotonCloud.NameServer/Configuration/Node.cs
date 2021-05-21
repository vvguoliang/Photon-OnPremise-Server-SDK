// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NodeConfig.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the NodeConfig type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
using Photon.Common.Authentication.Data;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PhotonCloud.NameServer.Configuration
{
    public class NodeList
    {
        [DataMember(IsRequired = true)]
        public List<Node> Nodes { get; set; }
    }
    /// <summary>
    /// The node config.
    /// </summary>
    public class Node : Photon.NameServer.Configuration.Node
    {
        [DataMember(IsRequired = true)]
        public List<ServiceType> ServiceTypes { get; set; }

        [DataMember(IsRequired = true)]
        public string PrivateCloud { get; set; }

        [DataMember(IsRequired = false)]
        public string Cluster { get; set; }

        [DataMember(IsRequired = false)]
        public bool UseV1Token { get; set; }
    }
}
