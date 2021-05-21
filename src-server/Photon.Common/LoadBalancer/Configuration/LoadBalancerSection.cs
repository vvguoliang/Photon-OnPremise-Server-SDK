// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LoadBalancerSection.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Configuration;
using Photon.Common.LoadBalancer.LoadShedding;

namespace Photon.Common.LoadBalancer.Configuration
{
    internal class LoadBalancerSection : ConfigurationSection
    {
        public LoadBalancerSection()
        {
            this.ValueDown = FeedbackLevel.Highest;
            this.ReserveRatio = 0.0f;
        }

        [ConfigurationProperty("LoadBalancerWeights", IsDefaultCollection = true, IsRequired = true)]
        public LoadBalancerWeightsCollection LoadBalancerWeights
        {
            get
            {
                return (LoadBalancerWeightsCollection)base["LoadBalancerWeights"];
            }
        }

        [ConfigurationProperty("ValueUp", IsRequired = true)]
        public FeedbackLevel ValueUp
        {
            get { return (FeedbackLevel)this["ValueUp"]; }
            set { this["ValueUp"] = value; }
        }

        [ConfigurationProperty("ValueDown", IsRequired = false)]
        public FeedbackLevel ValueDown
        {
            get { return (FeedbackLevel)this["ValueDown"]; }
            set { this["ValueDown"] = value; }
        }

        [ConfigurationProperty("ReserveRatio", IsRequired = false)]
        public float ReserveRatio
        {
            get { return (float)this["ReserveRatio"]; }
            set { this["ReserveRatio"] = value; }
        }

        public void Deserialize(System.Xml.XmlReader reader, bool serializeCollectionKey)
        {
            this.DeserializeElement(reader, serializeCollectionKey);
        }
    }
}
