// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Settings.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the Settings type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Photon.NameServer {


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

        [System.Configuration.ApplicationScopedSettingAttribute]
        [System.Diagnostics.DebuggerNonUserCodeAttribute]
        [System.Configuration.DefaultSettingValueAttribute("5055")]
        public ushort MasterServerPortUdp
        {
            get
            {
                return ((ushort)(this["MasterServerPortUdp"]));
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute]
        [System.Diagnostics.DebuggerNonUserCodeAttribute]
        [System.Configuration.DefaultSettingValueAttribute("4530")]
        public ushort MasterServerPortTcp
        {
            get
            {
                return ((ushort)(this["MasterServerPortTcp"]));
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute]
        [System.Diagnostics.DebuggerNonUserCodeAttribute]
        [System.Configuration.DefaultSettingValueAttribute("9090")]
        public ushort MasterServerPortWebSocket
        {
            get
            {
                return ((ushort)(this["MasterServerPortWebSocket"]));
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute]
        [System.Diagnostics.DebuggerNonUserCodeAttribute]
        [System.Configuration.DefaultSettingValueAttribute("80")]
        public ushort MasterServerPortHttp
        {
            get
            {
                return ((ushort)(this["MasterServerPortHttp"]));
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute]
        [System.Diagnostics.DebuggerNonUserCodeAttribute]
        [System.Configuration.DefaultSettingValueAttribute("443")]
        public ushort MasterServerPortSecureHttp
        {
            get
            {
                return ((ushort)(this["MasterServerPortSecureHttp"]));
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute]
        [System.Diagnostics.DebuggerNonUserCodeAttribute]
        [System.Configuration.DefaultSettingValueAttribute("photon/m")]
        public string MasterServerHttpPath
        {
            get
            {
                return ((string)(this["MasterServerHttpPath"]));
            }
        }

        //TODO add to config
        [System.Configuration.ApplicationScopedSettingAttribute]
        [System.Diagnostics.DebuggerNonUserCodeAttribute]
        [System.Configuration.DefaultSettingValueAttribute("7071")]
        public ushort MasterServerPortWebRTC
        {
            get
            {
                return ((ushort)(this["MasterServerPortWebRTC"]));
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute]
        [System.Diagnostics.DebuggerNonUserCodeAttribute]
        [System.Configuration.DefaultSettingValueAttribute("Nameserver.json")]
        public string NameServerConfig
        {
            get
            {
                return ((string)(this["NameServerConfig"]));
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute]
        [System.Diagnostics.DebuggerNonUserCodeAttribute]
        [System.Configuration.DefaultSettingValueAttribute("19090")]
        public ushort MasterServerPortSecureWebSocket
        {
            get
            {
                return ((ushort)(this["MasterServerPortSecureWebSocket"]));
            }
        }

        [System.Configuration.ApplicationScopedSettingAttribute]
        [System.Diagnostics.DebuggerNonUserCodeAttribute]
        [System.Configuration.DefaultSettingValueAttribute("true")]
        public bool EnablePerformanceCounters
        {
            get
            {
                return (bool)this["EnablePerformanceCounters"];
            }
        }
    }
}
