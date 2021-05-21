// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PluginSettings.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.Hive.Configuration
{
    using ExitGames.Logging;
    using Photon.SocketServer;
    using System;
    using System.Configuration;
    using System.IO;
    using System.Xml;

    public class PluginSettings : ConfigurationSection
    {
        #region Constants and Fields
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static PluginSettings defaultInstance;
        private static readonly string configPath = ApplicationBase.Instance.BinaryPath + @"\plugin.config";
        private static readonly object syncRoot = new object();
        private static string pluginSettingsHash = string.Empty;

        #endregion

        public delegate void ConfUpdatedEventHandler();
        public static event ConfUpdatedEventHandler ConfigUpdated;

        #region Constructors and Destructors

        static PluginSettings()
        {
            UpdateSettings();
            FileSystemWatcher watcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(configPath),
                Filter = Path.GetFileName(configPath),
                NotifyFilter = NotifyFilters.LastWrite
            };
            watcher.Changed += PluginConfigurationChanged;
            watcher.EnableRaisingEvents = true;
        }

        private static bool UpdateSettings()
        {
            lock (syncRoot)
            {
                var settings = new PluginSettings();
                try
                {
                    using (var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read))
                    {
                        var newHash = BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(stream));
                        if (pluginSettingsHash == newHash)
                        {
                            return false;
                        }
                        pluginSettingsHash = newHash;
                        stream.Position = 0;
                        
                        using (XmlReader reader = XmlReader.Create(stream))
                        {
                            settings.DeserializeSection(reader);
                        }
                    };
                }
                catch (Exception e)
                {
                    log.ErrorFormat("Failed to load plugin settings from file, using default one. File: {0} e: {1}", configPath, e);
                };
                defaultInstance = settings;
            }
            return true;
        }

        private static void PluginConfigurationChanged(object sender, FileSystemEventArgs e)
        {
            var result = UpdateSettings();
            if (ConfigUpdated != null && result)
            {
                ConfigUpdated();
            }
        }

        #endregion

        #region Properties

        public static PluginSettings Default
        {
            get
            {
                lock (syncRoot)
                {
                    return defaultInstance;
                }
            }
        }

        [ConfigurationProperty("Enabled", IsRequired = false, DefaultValue = "False")]
        public bool Enabled
        {
            get
            {
                return (bool)base["Enabled"];
            }

            set
            {
                base["Enabled"] = value;
            }
        }

        [ConfigurationProperty("Plugins", IsRequired = false)]
        [ConfigurationCollection(typeof(PluginElement), CollectionType = ConfigurationElementCollectionType.BasicMap)]
        public PluginElementCollection Plugins
        {
            get
            {
                return (PluginElementCollection)base["Plugins"];
            }
        }

        #endregion
    }
}