// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Settings.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the Settings type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace PhotonCloud.NameServer {


    internal sealed class Settings : System.Configuration.ApplicationSettingsBase
    {
        private static Settings defaultInstance = ((Settings)(Synchronized(new Settings())));

        public static Settings Default
        {
            get
            {
                return defaultInstance;
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.Configuration.DefaultSettingValueAttribute("Nameserver")]
        public string CloudType
        {
            get
            {
                return ((string)(this["CloudType"]));
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.Configuration.DefaultSettingValueAttribute("Public")]
        public string PrivateCloud
        {
            get
            {
                return ((string)(this["PrivateCloud"]));
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.Configuration.DefaultSettingValueAttribute("EU")]
        public string Region
        {
            get
            {
                return ((string)(this["Region"]));
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.Configuration.DefaultSettingValueAttribute("Default")]
        public string Cluster
        {
            get
            {
                return ((string)(this["Cluster"]));
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.Configuration.DefaultSettingValueAttribute("203.0.113.0")]
        public string IPv4NullAddress
        {
            get
            {
                return ((string)(this["IPv4NullAddress"]));
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.Configuration.DefaultSettingValueAttribute("100::")]
        public string IPv6NullAddress
        {
            get
            {
                return ((string)(this["IPv6NullAddress"]));
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.Configuration.DefaultSettingValueAttribute("false")]
        public bool UseEncryptionQueue
        {
            get
            {
                return (bool)this["UseEncryptionQueue"];
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.Configuration.DefaultSettingValueAttribute("1000")]
        public int EncryptionQueueLimit
        {
            get
            {
                return (int)this["EncryptionQueueLimit"];
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.Configuration.DefaultSettingValueAttribute("http://internal-health.photonengine.com/photon/h2/health/check?names={0}")]
        public string MonitoringApiEndpoint
        {
            get
            {
                return ((string)this["MonitoringApiEndpoint"]);
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.Configuration.DefaultSettingValueAttribute("60")]
        public int MonitoringCacheUpdateInterval
        {
            get
            {
                return (int)this["MonitoringCacheUpdateInterval"];
            }
        }
    }
}
