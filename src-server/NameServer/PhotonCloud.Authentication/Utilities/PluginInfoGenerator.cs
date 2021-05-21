using System;
using System.Collections.Generic;
using Photon.Common.Plugins;
using PhotonCloud.Authentication.Data;

namespace PhotonCloud.Authentication.Utilities
{
    public static class PluginInfoGenerator
    {
        public static bool ParseJsonConfig(ExternalApiInfoList configurationInfo, out PluginInfo info, out string errorMsg)
        {
            errorMsg = string.Empty;
            if (ExternalApiInfoList.IsNullOrEmpty(configurationInfo))
            {
                info = new PluginInfo();
                return true;
            }

            var pluginInfo = new PluginInfo();
            // only one Plugin for now:     
            var config = configurationInfo.Entries[0];

            if (config.ApiName == "CustomExtension")
            {
                if (config.ApiValues.FindAll(a => a.Name == "Type" || a.Name == "Version" || a.Name == "AssemblyName" || a.Name == "Path").Count != 4)
                {
                    info = new PluginInfo();
                    errorMsg = "CustomExtension configuration expects: Type, Version, AssemblyName and Path";
                    return false;
                }

                var path = config.ApiValues.Find(a => a.Name == "Path").Value;
                if (!pluginInfo.SetCustomPath(path))
                {
                    info = new PluginInfo();
                    errorMsg = "Invalid Path in configuration: " + (path ?? "'null'");
                    return false;
                }
            }
            else
            {
                if (config.ApiValues.FindAll(a => a.Name == "Type" || a.Name == "Version" || a.Name == "AssemblyName").Count != 3)
                {
                    info = new PluginInfo();
                    errorMsg = "Plugin configuration expects: Type, Version and AssemblyName";
                    return false;
                }
            }
            if (config.ApiValues.FindAll(a => a.Name == "Path").Count == 1)
            {
                pluginInfo.Name = config.ApiValues.Find(a => a.Name == "Path").Value;
            }
            else
            {
                pluginInfo.Name = config.ApiName;
            }
            pluginInfo.Type = config.ApiValues.Find(a => a.Name == "Type").Value;
            pluginInfo.Version = config.ApiValues.Find(a => a.Name == "Version").Value;
            pluginInfo.AssemblyName = config.ApiValues.Find(a => a.Name == "AssemblyName").Value;

            pluginInfo.ConfigParams = new Dictionary<string, string>();
            var customApiValues = config.ApiValues.FindAll(a => a.Name != "Type" && a.Name != "Version" && a.Name != "AssemblyName" && a.Name != "Path");
            foreach (var customApiValue in customApiValues)
            {
                pluginInfo.ConfigParams[customApiValue.Name] = customApiValue.Value;
            }

            info = pluginInfo;
            return true;
        }
    }
}