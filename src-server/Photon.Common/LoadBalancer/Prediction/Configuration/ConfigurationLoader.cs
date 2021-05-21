using System;
using System.Xml;
using Photon.Common.LoadBalancer.LoadShedding.Configuration;

namespace Photon.Common.LoadBalancer.Prediction.Configuration
{
    internal static class ConfigurationLoader 
    {
        public static bool TryLoadFromFile(string fileName, out LoadPredictionSystemSection section, out string message)
        {
            section = null;
            message = string.Empty;

            try
            {
                section = LoadFromFile(fileName);
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private static LoadPredictionSystemSection LoadFromFile(string fileName)
        {
            using (var fileStream = System.IO.File.Open(fileName, System.IO.FileMode.Open))
            {
                var xmlReader = XmlReader.Create(fileStream);
                xmlReader.MoveToContent();

                var graphSection = new LoadPredictionSystemSection();
                graphSection.Deserialize(xmlReader, false);

                fileStream.Close();

                return graphSection;
            }
        }
    }
}
