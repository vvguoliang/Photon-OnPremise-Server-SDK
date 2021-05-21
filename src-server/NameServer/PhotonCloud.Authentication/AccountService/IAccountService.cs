// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IAuthenticator.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using Photon.Common.Authentication;

namespace PhotonCloud.Authentication.AccountService
{
    using PhotonCloud.Authentication.Data;

    public interface IAccountService
    {
        int Timeout { get;  }

        bool FormatApplicationId(string applicationId, out string formattedAppId);

        void TryGetExternalApiInfo(string appId, Action<AccountServiceResult, ExternalApiInfoList> onGetExteralApiInfoCallback);
        void VerifyVAppsAccount(string applicationId, bool allowOnFailure, Action<ApplicationAccount> callback);
    }
}
