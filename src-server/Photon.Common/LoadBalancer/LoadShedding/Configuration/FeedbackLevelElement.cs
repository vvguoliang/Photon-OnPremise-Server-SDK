// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FeedbackLevelElement.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Configuration;

namespace Photon.Common.LoadBalancer.LoadShedding.Configuration
{
    internal class FeedbackLevelElement : ConfigurationElement
    {
        internal FeedbackLevelElement()
        {
            this.ValueDown = -1;
        }

        [ConfigurationProperty("Level", IsRequired = true)]
        public FeedbackLevel Level
        {
            get { return (FeedbackLevel)this["Level"]; }
            set { this["Level"] = value; }
        }

        [ConfigurationProperty("Value", IsRequired = true)]
        public int Value
        {
            get { return (int)this["Value"]; }
            set { this["Value"] = value; }
        }

        [ConfigurationProperty("ValueDown", IsRequired = false)]
        public int ValueDown
        {
            get { return (int)this["ValueDown"]; }
            set { this["ValueDown"] = value; }
        }
    }
}
