
using System;
using Photon.Common.Authentication;

namespace PhotonCloud.Authentication
{
    using System.Collections.Generic;
    using System.Linq;
    using PhotonCloud.Authentication.Data;
    using Photon.Common.Authentication.Data;

    [Flags]
    public enum ClientConnectionFlagsType
    {
        None,
        RequireSecuredConnection = 0x01,
    }


    /// <summary>
    /// Details of the Application Account (result from accountService.GetAccount), inherits AuthSettings.
    /// </summary>
    public class ApplicationAccount : AuthSettings
    {
        /// <summary>
        /// Default ctor for serialization
        /// </summary>
        public ApplicationAccount() { }

        public ApplicationAccount(string applicationId, AccountServiceResult authResult, bool isAuthenticated, string debugMessage)
        {
            this.ApplicationId = applicationId;
            this.AccountServiceResult = authResult;
            this.IsAuthenticated = isAuthenticated;
            this.DebugMessage = debugMessage;

            // if the CCU is not set explicitly, assume that we have "unlimited" access.  CCU = 0 does not mean "unlimited" any longer. 
            this.MaxCcu = int.MaxValue;
        }

        public ApplicationAccount(string applicationId, AccountServiceResult authResult, bool isAuthenticated, string debugMessage, int maxCcu, bool isCcuBurstAllowed,
            string privateCloud, bool isAnonymousAccessAllowed, ServiceType serviceType)
        {
            this.ApplicationId = applicationId;
            this.AccountServiceResult = authResult;
            this.IsAuthenticated = isAuthenticated;
            this.MaxCcu = maxCcu;
            this.IsCcuBurstAllowed = isCcuBurstAllowed;
            this.PrivateCloud = privateCloud.ToLower();
            this.IsAnonymousAccessAllowed = isAnonymousAccessAllowed;
            this.ServiceType = serviceType;
            this.ExternalApiList = new ExternalApiInfoList();
            this.DebugMessage = debugMessage;
        }

        public ApplicationAccount(string aplicationId, AccountServiceResult authResult, TmpApplicationAccount tmpApplicationAccount)
        {
            this.ApplicationId = aplicationId;
            this.AccountServiceResult = authResult;
            this.IsAuthenticated = tmpApplicationAccount.ReturnCode == (int)AccountServiceReturnValue.Success; //TODO:
            this.MaxCcu = tmpApplicationAccount.ApplicationCcu;
            this.IsCcuBurstAllowed = tmpApplicationAccount.ApplicationCcuBurst;
            this.PrivateCloud = tmpApplicationAccount.PrivateCloud?.ToLower();
            this.IsAnonymousAccessAllowed = tmpApplicationAccount.ClientAuthenticationAllowAnonymous;
            this.ServiceType = tmpApplicationAccount.ServiceType;
            this.ExternalApiList = tmpApplicationAccount.ExternalApiInfoList;
            this.DebugMessage = tmpApplicationAccount.Message;
            this.ClientAuthTokenLevel = tmpApplicationAccount.ClientAuthTokenLevel;
            this.VirtualAppVersionsCountLimit = tmpApplicationAccount.VirtualAppVersionsCountLimit;

            this.GameListUseLegacyLobbies = tmpApplicationAccount.GameListUseLegacyLobbies;
            this.GameListLimit = tmpApplicationAccount.GameListLimit;
            this.GameListLimitUpdates = tmpApplicationAccount.GameListLimitUpdates;
            this.GameListLimitSqlFilterResults = tmpApplicationAccount.GameListLimitSqlFilterResults;

            this.ClientConnectionFlags = tmpApplicationAccount.ClientConnectionFlags;
			
			this.MatchmakingStoredProcedure = tmpApplicationAccount.MatchmakingStoredProcedure;

            if (this.HasExternalApi)
            {
                this.TrimExternalApiListStrings();
            }

            if (!string.IsNullOrEmpty(tmpApplicationAccount.GetRegionsFilter))
            {
                //always remove spaces, convert to lower case and add a semicolon at end
                this.GetRegionsFilter = tmpApplicationAccount.GetRegionsFilter.Replace(" ", "").ToLower() + ";";
            }

            if (!string.IsNullOrEmpty(tmpApplicationAccount.RegionClusterInfo))
            {
                // e.g.:  jp/cluster2;jp/cluster3;us/experimental
                this.RegionClusterInfos = new Dictionary<string, List<string>>();
                var regionClusterInfos = tmpApplicationAccount.RegionClusterInfo.ToLower().Split(',', ';');

                foreach (var info in regionClusterInfos)
                {
                    var regionCluster = info.Split('/');

                    if (regionCluster.Length != 2)
                    {
                        continue;
                    }

                    string region = regionCluster[0];
                    string cluster = regionCluster[1];
                    if (RegionClusterInfos.ContainsKey(region))
                    {
                        var l = RegionClusterInfos[region];
                        if (!l.Contains(cluster))
                        {
                            l.Add(cluster);
                        }
                    }
                    else
                    {
                        RegionClusterInfos[region] = new List<string> { cluster };
                    }
                }
            }


            if (tmpApplicationAccount.ClientAuthenticationServiceInfoList != null && tmpApplicationAccount.ClientAuthenticationServiceInfoList.TotalRecordCount > 0)
            {
                this.IsClientAuthenticationEnabled = true;
                this.ClientAuthenticationServices = tmpApplicationAccount.ClientAuthenticationServiceInfoList.Entries;
            }
        }

        public string ApplicationId { get; set; }

        public AccountServiceResult AccountServiceResult { get; set; }

        public bool IsAuthenticated { get; set; }

        public string DebugMessage { get; set; }

        public int MaxCcu { get; set; }

        public bool IsCcuBurstAllowed { get; set; }

        public string PrivateCloud { get; set; }

        public Dictionary<string, List<string>> RegionClusterInfos { get; set; }

        public bool IsClientAuthenticationEnabled { get; set; }

        public List<ClientAuthenticationServiceInfo> ClientAuthenticationServices { get; set; }

        public ServiceType ServiceType { get; set; }

        public string GetRegionsFilter { get; set; }

        public int ClientAuthTokenLevel { get; set; }

        public int VirtualAppVersionsCountLimit { get; private set; }

        public bool? GameListUseLegacyLobbies { get; set; }
        public int? GameListLimit { get; set; }
        public int? GameListLimitUpdates { get; set; }
        public int? GameListLimitSqlFilterResults { get; set; }

        /// <summary>
        /// different requirements to client connection
        /// </summary>
        public byte ClientConnectionFlags { get; }

        public bool RequireSecureConnection => (this.ClientConnectionFlags & (byte)ClientConnectionFlagsType.RequireSecuredConnection) != 0;
		
		public string MatchmakingStoredProcedure { get; set; }	

         public bool HasExternalApi
        {
            get
            {
                return this.ExternalApiList != null && this.ExternalApiList.Entries != null && this.ExternalApiList.Entries.Count > 0;
            }
        }

        public ExternalApiInfoList ExternalApiList { get; set; }

        public bool IsAuthenticatedForPrivateCloud(string privateCloud)
        {
            // if we have not specified a privateCloud to check - don't check. (e.g., no PrivateCloud set in app.config) 
            if (string.IsNullOrEmpty(privateCloud))
            {
                return true;
            }

            // if the account service has not specified a private cloud, the app is "free for all". 
            if (string.IsNullOrEmpty(this.PrivateCloud))
            {
                return true;
            }

            // allowed for all (does not make much sense - we should restrict to either public or a special private cloud - but just in case...) 
            if (System.String.Compare(this.PrivateCloud, "all", System.StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }

            // allowed only for this private cloud
            if (System.String.Compare(this.PrivateCloud, privateCloud, System.StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }

            // not allowed for this cloud
            return false;
        }

        public bool IsAuthenticatedForServiceType(ServiceType[] serviceTypes)
        {
            // allowed for one of this service types
            if (serviceTypes.Contains(this.ServiceType))
            {
                return true;
            }

            // not allowed for this cloud
            return false;
        }

        public bool IsClientAuthenticationRequired
        {
            get { return this.IsClientAuthenticationEnabled && !this.IsAnonymousAccessAllowed; }
        }
        public bool IsEnterprise
        {
            get
            {
                return this.PrivateCloud != "public" && this.PrivateCloud != "quantum";
            }
        }

        public bool IsAppForCluster0
        {
            get
            {
                return this.MaxCcu < 500 && !this.IsEnterprise;
            }
        }

        #region Methods


        private void TrimExternalApiListStrings()
        {
            foreach (var externalApiInfo in this.ExternalApiList.Entries)
            {
                foreach (var value in externalApiInfo.ApiValues)
                {
                    value.Name = value.Name.Trim();
                }
            }
        }

        #endregion
    }
}
