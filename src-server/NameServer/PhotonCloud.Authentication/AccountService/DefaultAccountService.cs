// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DefaultAuthentication.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using Photon.Common.Authentication;
using Photon.Common.Authentication.Data;

namespace PhotonCloud.Authentication.AccountService
{
    using System.Collections.Generic;
    using PhotonCloud.Authentication.Data;
    /// <summary>
    /// This handler is ONLY used for testing. It assumes that the default values are "public" cloud, any region. 
    /// </summary>
    public class DefaultAccountService : IAccountService
    {
        private const string DefaultCloud = "Public";

        public DefaultAccountService(int timeout)
        {
            this.Timeout = timeout;
        }

        public int Timeout { get; private set; }

        public bool FormatApplicationId(string applicationId, out string formattedAppId)
        {
            formattedAppId = applicationId.Trim().ToLower();
            return true;
        }

        public ApplicationAccount VerifyVAppsAccount(string appId, bool allowOnFailure)
        {
            var result = new ApplicationAccount(appId, AccountServiceResult.Ok, true, "OK", int.MaxValue, false,
                DefaultCloud, true, ServiceType.Realtime)
            {
                IsClientAuthenticationEnabled = true,
                ClientAuthenticationServices = new List<ClientAuthenticationServiceInfo>
                {
                    new ClientAuthenticationServiceInfo(ClientAuthenticationType.Custom,
                        "http://localhost:50925/Account/Authenticate", null)
                },
                ExternalApiList = this.GetExternalApiInfo(),

                // adding clusters for US region in order to test sharding:
                RegionClusterInfos = new Dictionary<string, List<string>> {{"us", new List<string> {"cluster2", "cluster3"}}}
            };

            return result;
        }

        public bool TryGetExternalApiInfo(string application, out ExternalApiInfoList apiList)
        {
            apiList = this.GetExternalApiInfo(); // new ExternalApiInfoList();
            return true;
        }

        public void TryGetExternalApiInfo(string appId, Action<AccountServiceResult, ExternalApiInfoList> onGetExteralApiInfoCallback)
        {
            onGetExteralApiInfoCallback(AccountServiceResult.Ok, this.GetExternalApiInfo());
        }

        public void VerifyVAppsAccount(string applicationId, bool allowOnFailure, Action<ApplicationAccount> onGetApplicationAccount)
        {
            onGetApplicationAccount(VerifyVAppsAccount(applicationId, allowOnFailure));
        }

        private ExternalApiInfoList GetExternalApiInfo()
        {
            return new ExternalApiInfoList();

            //var apiInfo = new ExternalApiInfo() { ApiName = "Test" };
            //apiInfo.ApiValues = new List<ExternalApiValue>();

            //// RPC plugin
            //var apiValue = new ExternalApiValue() { Mandatory = true, Name = "BaseUrl", Value = "https://www.google.de/" };
            //apiInfo.ApiValues.Add(apiValue);

            //apiValue = new ExternalApiValue() { Mandatory = true, ReadOnlyValue = true, Name = "Type", Value = "Lite" };
            //apiInfo.ApiValues.Add(apiValue);
            //apiValue = new ExternalApiValue() { Mandatory = true, ReadOnlyValue = true, Name = "Version", Value = "1.0.0.0" };
            //apiInfo.ApiValues.Add(apiValue);
            //apiValue = new ExternalApiValue() { Mandatory = true, ReadOnlyValue = true, Name = "AssemblyName", Value = "Lite.dll" };
            //apiInfo.ApiValues.Add(apiValue);

            //var list = new ExternalApiInfoList();
            //list.Entries = new List<ExternalApiInfo>();
            //list.Entries.Add(apiInfo);
            //list.TotalRecordCount = 1;
            //return list;
        }
    }
}
