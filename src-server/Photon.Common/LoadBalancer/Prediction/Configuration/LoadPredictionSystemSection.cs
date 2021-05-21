// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FeedbackControlSystemSection.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Configuration;
using Photon.Common.LoadBalancer.LoadShedding.Configuration;

namespace Photon.Common.LoadBalancer.Prediction.Configuration
{
    internal class LoadPredictionSystemSection : ConfigurationSection
    {
        [ConfigurationProperty("FeedbackControllers", IsDefaultCollection = true, IsRequired = true)]
        public FeedbackControllerElementCollection FeedbackControllers
        {
            get
            {
                return (FeedbackControllerElementCollection)base["FeedbackControllers"];
            }

            set
            {
                base["FeedbackControllers"] = value;
            }
        }

        public void Deserialize(System.Xml.XmlReader reader, bool serializeCollectionKey)
        {
            this.DeserializeElement(reader, serializeCollectionKey);
        }
    }
}
