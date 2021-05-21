// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ApplicationInfo.cs" company="">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace PhotonCloud.Authentication.Data
{
    using Photon.Common.Authentication.Data;
    using System;
    using System.Runtime.Serialization;

    public class TmpApplicationAccount// : LBApplicationInfo
    {
        /// <summary>
        ///  When AppId is not found account service returns "00000000-0000-0000-0000-000000000000", ApplicationCcu = 0, ReturnCode != 0
        /// </summary>
        [DataMember(IsRequired = true)]
        public Guid ApplicationId { get; set; }
        
        [DataMember(IsRequired = false)]
        public int ReturnCode { get; set; }

        [DataMember(IsRequired = false)]
        public string Message { get; set; }

        [DataMember(IsRequired = false)]
        public int ApplicationCcu { get; set; }

        [DataMember(IsRequired = false)]
        public bool ApplicationCcuBurst { get; set; }

        [DataMember(IsRequired = false)]
        public string ApplicationRegion { get; set; }

        [DataMember(IsRequired = false)]
        public string PrivateCloud { get; set; }

        [DataMember(IsRequired = false)]
        public string RegionClusterInfo { get; set; }

        [DataMember(IsRequired = false)]
        public bool ClientAuthenticationAllowAnonymous { get; set; }

        [DataMember(IsRequired = false)]
        public ClientAuthenticationServiceInfoList ClientAuthenticationServiceInfoList { get; set; }

        [DataMember(IsRequired = false)]
        public ExternalApiInfoList ExternalApiInfoList { get; set; }

        [DataMember(IsRequired = false)]
        public ServiceType ServiceType { get; set; }

        [DataMember(IsRequired = false)]
        public string GetRegionsFilter { get; set; }

        [DataMember(IsRequired = false)]
        public int ClientAuthTokenLevel { get; set; }

        [DataMember(IsRequired = false)]
        public int VirtualAppVersionsCountLimit { get; set; }

        [DataMember(IsRequired = false)]
        public bool? GameListUseLegacyLobbies { get; set; }
        [DataMember(IsRequired = false)]
        public int? GameListLimit { get; set; }
        [DataMember(IsRequired = false)]
        public int? GameListLimitUpdates { get; set; }
        [DataMember(IsRequired = false)]
        public int? GameListLimitSqlFilterResults { get; set; }

        [DataMember(IsRequired = false)]
        public byte ClientConnectionFlags { get; set; }
		
        [DataMember(IsRequired = false)]
        public string MatchmakingStoredProcedure { get; set; }
		
        // all other application account is not relevant for Photon
    }
}
