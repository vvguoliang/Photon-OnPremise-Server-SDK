namespace Photon.LoadBalancing.MasterServer
{
    using System.Configuration;
    using System.Diagnostics;

    public sealed class MasterServerSettings : ApplicationSettingsBase
    {
        #region Static Fields

        private static readonly MasterServerSettings defaultInstance =
            ((MasterServerSettings)(Synchronized(new MasterServerSettings())));

        #endregion

        #region Public Properties

        public static MasterServerSettings Default
        {
            get
            {
                return defaultInstance;
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("5000")]
        public int AppStatsPublishInterval
        {
            get
            {
                return ((int)(this["AppStatsPublishInterval"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("any_id_you_like_here")]
        public string ApplicationId
        {
            get
            {
                return ((string)(this["ApplicationId"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("1.0")]
        public string ApplicationVersion
        {
            get
            {
                return ((string)(this["ApplicationVersion"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("False")]
        public bool EnableProxyConnections
        {
            get
            {
                return ((bool)(this["EnableProxyConnections"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("1000")]
        public int GameChangesPublishInterval
        {
            get
            {
                return ((int)(this["GameChangesPublishInterval"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("1")]
        public int GameExpiryCheckPeriod
        {
            get
            {
                return ((int)(this["GameExpiryCheckPeriod"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("0")]
        public int GameListLimit
        {
            get
            {
                return ((int)(this["GameListLimit"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("4520")]
        public int IncomingGameServerPeerPort
        {
            get
            {
                return ((int)(this["IncomingGameServerPeerPort"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("0")]
        public int LobbyStatsLimit
        {
            get
            {
                return ((int)(this["LobbyStatsLimit"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("120")]
        public int LobbyStatsPublishInterval
        {
            get
            {
                return ((int)(this["LobbyStatsPublishInterval"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("0")]
        public int MasterRelayPortTcp
        {
            get
            {
                return ((int)(this["MasterRelayPortTcp"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("0")]
        public int MasterRelayPortUdp
        {
            get
            {
                return ((int)(this["MasterRelayPortUdp"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("0")]
        public int MasterRelayPortWebSocket
        {
            get
            {
                return ((int)(this["MasterRelayPortWebSocket"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("60")]
        public int PersistentGameExpiryMinute
        {
            get
            {
                return ((int)(this["PersistentGameExpiryMinute"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("")]
        public string PublicIPAddress
        {
            get
            {
                return ((string)(this["PublicIPAddress"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("")]
        public string RedisDB
        {
            get
            {
                return ((string)(this["RedisDB"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("1200000")]
        public int DisconnectIfNoAuthInterval
        {
            get
            {
                return ((int) (this["DisconnectIfNoAuthInterval"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("False")]
        public bool UseLegacyLobbies
        {
            get
            {
                return ((bool)(this["UseLegacyLobbies"]));
            }
        }

        //0 is not unlimited!
        //this setting was added to not change the exising GameListLimit behaviour (used at JoinLobby, 0 is unlimited)
        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("500")]
        public int LimitGameList
        {
            get
            {
                return (int)(this["LimitGameList"]);
            }
        }

        //0 is not unlimited!
        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("500")]
        public int LimitGameListUpdate
        {
            get
            {
                return (int)(this["LimitGameListUpdate"]);
            }
        }

        //0 is unlimited
        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("100")]
        public int LimitSqlFilterResults
        {
            get
            {
                return ((int)(this["LimitSqlFilterResults"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("20000")]
        public int GSContextTTL
        {
            get
            {
                return ((int)(this["GSContextTTL"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("10000")]
        public int LobbiesCountLimit
        {
            get
            {
                return (int)this["LobbiesCountLimit"];
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("1800000")]//30 min
        public int ReplicationFinishCheckInterval
        {
            get
            {
                return (int)this["ReplicationFinishCheckInterval"];
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("True")]
        public bool OnlyLogQueryDataErrors
        {
            get
            {
                return ((bool)(this["OnlyLogQueryDataErrors"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("ALTER;CREATE;DELETE;DROP;EXEC;EXECUTE;INSERT;INSERT INTO;MERGE;SELECT;UPDATE;UNION;UNION ALL")]
        public string WrongWordsForQueryData
        {
            get
            {
                return ((string)(this["WrongWordsForQueryData"]));
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("3")]
        public int MaxConcurrentJoinRequests
        {
            get
            {
                return (int)this["MaxConcurrentJoinRequests"];
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("4")]
        public int MaxTotalJoinRequests
        {
            get
            {
                return (int)this["MaxTotalJoinRequests"];
            }
        }

        [ApplicationScopedSetting]
        [DebuggerNonUserCode]
        [DefaultSettingValue("false")]
        public bool AllowDebugGameOperation
        {
            get
            {
                return ((bool)(this["AllowDebugGameOperation"]));
            }
        }

        #endregion
    }
}