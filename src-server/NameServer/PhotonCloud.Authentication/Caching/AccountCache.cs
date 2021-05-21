// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AuthenticateCache.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the AuthenticateCache type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics;
using ExitGames.Concurrency.Fibers;
using Photon.Common.Authentication;
using Photon.SocketServer.Diagnostics;

namespace PhotonCloud.Authentication.Caching
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;

    using ExitGames.Logging;
    using ExitGames.Threading;
    using Photon.SocketServer;
    using Photon.VirtualApps.Master.Caching;

    using PhotonCloud.Authentication.AccountService;

    using Settings = PhotonCloud.Authentication.Settings;

    public class AccountCache
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly IAccountService accountService;

        private readonly Dictionary<string, CachedResult> dictionary;

        private readonly TimeSpan refreshInterval;

        private readonly bool allowOnFailure; 

        protected readonly HashSet<PeerBase> Subscribers = new HashSet<PeerBase>();

        #endregion

        #region Constructors and Destructors

        protected AccountCache(TimeSpan updateInterval, IAccountService accountService, bool allowOnFailure)
        {
            this.refreshInterval = updateInterval;
            this.dictionary = new Dictionary<string, CachedResult>();
            this.accountService = accountService;
            this.allowOnFailure = allowOnFailure; 

            if (log.IsInfoEnabled)
            {
                log.InfoFormat("AuthenticationCache created: updateInterval={0}, allowOnFailure={1}", updateInterval, allowOnFailure);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Create cache
        /// </summary>
        /// <param name="authenticationHandler"></param>
        /// <param name="allowOnFailure">Store cached result even if auth failed with <see cref="AccountServiceResult.Error"/> or <see cref="AccountServiceResult.Timeout"/>/></param>
        /// <returns></returns>
        public static AccountCache CreateCache(IAccountService authenticationHandler, bool allowOnFailure)
        {

            TimeSpan updateInterval;
            if (Settings.Default.AuthCacheUpdateInterval <= 0)
            {
                log.WarnFormat("Invalid update interval {0} specified for AuthenticateCache. Using default value.");
                updateInterval = TimeSpan.FromSeconds(300);
            }
            else
            {
                updateInterval = TimeSpan.FromSeconds(Settings.Default.AuthCacheUpdateInterval);
            }

            return new AccountCache(updateInterval, authenticationHandler, allowOnFailure);
        }

        public void AddSubscriber(PeerBase subscriber)
        {
            using (Lock.TryEnter(this.Subscribers, 10000))
            {
                this.Subscribers.Add(subscriber);
            }
        }

        public void GetAccount(string applicationId, IFiber fiber, Action<ApplicationAccount> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException("callback");
            }

            if (fiber == null)
            {
                throw new ArgumentNullException("fiber");
            }

            var tmp = callback;
            callback = account => fiber.Enqueue(() => tmp(account));

            if (applicationId == null)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("Failed to authenticate. Application id is null.");
                }

                callback(new ApplicationAccount(string.Empty, AccountServiceResult.Error, false, ErrorMessages.AppIdMissing));
                return;
            }

            applicationId = applicationId.Trim();
            if (string.IsNullOrEmpty(applicationId))
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("Failed to authenticate. app id is Empty");
                }

                callback(new ApplicationAccount(string.Empty, AccountServiceResult.Error, false, ErrorMessages.EmptyAppId));
                return;
            }
            
            string appId;
            if (!this.accountService.FormatApplicationId(applicationId, out appId))
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Failed to authenticate. Invalid application id format: {0}", applicationId);
                }

                callback(new ApplicationAccount(string.Empty, AccountServiceResult.Error, false, ErrorMessages.InvalidAppIdFormat));
                return;
            }

            CachedResult result;
            using (Lock.TryEnter(this.dictionary, 10000))
            {
                if (!this.dictionary.TryGetValue(appId, out result))
                {
                    log.DebugFormat("Create cached auth result for app: {0}", appId);
                    result = new CachedResult(this, appId, this.refreshInterval);
                    this.dictionary.Add(appId, result);
                }
            }

            result.GetResult(callback);
        }

        public Hashtable GetAll()
        {
            var hashtable = new Hashtable(this.dictionary.Count);

            using (Lock.TryEnter(this.dictionary, 10000))
            {
                foreach (var item in this.dictionary.Values)
                {
                    CacheItem<ApplicationAccount> cachedItem = item.CachedItem;
                    if (cachedItem != null && cachedItem.Value != null)
                    {
                        var authResult = cachedItem.Value;

                        var isClientAuthenticationRequired = authResult.IsClientAuthenticationEnabled && !authResult.IsAnonymousAccessAllowed;
                        var hasExternalApi = authResult.HasExternalApi;

                        hashtable.Add(
                            item.ApplicationId,
                            new object[] { 
                                authResult.IsAuthenticated, 
                                isClientAuthenticationRequired,
                                authResult.IsClientAuthenticationEnabled, 
                                hasExternalApi});
                    }
                }
            }

            return hashtable;
        }

        public void RemoveSubscriber(PeerBase subscriber)
        {
            using (Lock.TryEnter(this.Subscribers, 10000))
            {
                this.Subscribers.Remove(subscriber);
            }
        }

        #endregion

        #region Methods

        protected virtual void OnAuthenticateResultUpdated(string appId, ApplicationAccount appAccount)
        {
        }

        private class CachedResult
        {
            #region Constants and Fields

            public readonly string ApplicationId;

            private readonly AccountCache cache;

            private readonly object syncLock = new object();

            private TimeSpan updateInterval;

            private readonly TimeSpan initialUpdateInterval;

            private CacheItem<ApplicationAccount> cachedItem;

            private event Action<ApplicationAccount> callbacks;

            private readonly Random random = new Random();

            private readonly LogCountGuard logCountGuard = new LogCountGuard(new TimeSpan(0, 1, 0));
            #endregion

            #region Constructors and Destructors

            public CachedResult(AccountCache cache, string applicationId, TimeSpan updateInterval)
            {
                this.cache = cache;
                this.ApplicationId = applicationId;
                this.updateInterval = updateInterval;
                this.initialUpdateInterval = updateInterval;
            }

            #endregion

            #region Properties

            public CacheItem<ApplicationAccount> CachedItem
            {
                get
                {
                    return this.cachedItem;
                }
            }

            private TimeSpan UpdateInterval { get { return this.updateInterval; } }
            #endregion

            #region Public Methods

            public void GetResult(Action<ApplicationAccount> callback)
            {
                // check if there is already a cache result and the result is still valid
                var cachedResult = this.cachedItem;
                if (cachedResult != null && cachedResult.ItemAge < this.UpdateInterval)
                {
                    // the cached result is still valid and can be returned
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("The cached result is still valid and can be returned. AppId: {0}, Cloud: {1}", cachedResult.Value.ApplicationId, cachedResult.Value.PrivateCloud);
                    }

                    callback(cachedResult.Value);
                    return;
                }

                lock (this.syncLock)
                {
                    if (this.callbacks != null)// != null means we already wait for response
                    {
                        this.callbacks += callback;// add one more callback to set of callbacks
                        return;
                    }

                    cachedResult = this.cachedItem;// cache value to use it to call callback if it is valid.
                    if (cachedResult == null || cachedResult.ItemAge >= this.UpdateInterval)
                    {
                        // cached value is not valid we set callback and reset cachedResult. 
                        // this means we need send request to account service
                        this.callbacks = callback;
                        cachedResult = null;
                    }
                }

                if (cachedResult != null)
                {
                    // while we were in lock region cache was updated. so we call callback
                    callback(cachedResult.Value);
                    return;
                }

                // requesting data from account service
                this.cache.accountService.VerifyVAppsAccount(this.ApplicationId, this.cache.allowOnFailure, this.OnGetApplicationAccount);
            }

            #endregion

            #region Methods

            private void SetCacheItem(CacheItem<ApplicationAccount> currentItem, ApplicationAccount authResult)
            {
                var newCachedItem = new CacheItem<ApplicationAccount>(authResult);
                var oldItem = Interlocked.CompareExchange(ref this.cachedItem, newCachedItem, currentItem);
                if (oldItem == currentItem)
                {
                    this.cache.OnAuthenticateResultUpdated(this.ApplicationId, authResult);

                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Updated cached auth result for {0}: {1}", this.ApplicationId, authResult.IsAuthenticated);
                    }
                }
            }

            private void OnGetApplicationAccount(ApplicationAccount applicationAccount)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Authenticating: appId={0}", this.ApplicationId);
                }

                if (log.IsDebugEnabled)
                {
                    log.DebugFormat(
                        "Authenticate: appId={0}, result={1}, ccu={2}, burst={3}, cloud={4}",
                        this.ApplicationId,
                        applicationAccount.IsAuthenticated,
                        applicationAccount.MaxCcu,
                        applicationAccount.IsCcuBurstAllowed,
                        applicationAccount.PrivateCloud);
                    log.DebugFormat(
                       "GameListLimits: UseLegacyLobbies={0}, GameListLimit={1}, GameListLimitUpdates={2}, GameListLimitSqlFilterResults={3}",
                       applicationAccount.GameListUseLegacyLobbies,
                       applicationAccount.GameListLimit,
                       applicationAccount.GameListLimitUpdates,
                       applicationAccount.GameListLimitSqlFilterResults);
                }

                // store in cache EITHER if the authentication call succeeded OR 
                // if we allow "failed" auth calls. 
                switch (applicationAccount.AccountServiceResult)
                {
                    case AccountServiceResult.Ok:
                        this.SetCacheItem(this.cachedItem, applicationAccount);
                        this.updateInterval = this.initialUpdateInterval;
                        break;

                    default:
                        if (applicationAccount.AccountServiceResult == AccountServiceResult.NotFound)
                        {
                            log.DebugFormat("Account NOT found. appId:'{0}'", this.ApplicationId);
                        }

                        if (this.cache.allowOnFailure)
                        {
                            this.SetCacheItem(this.cachedItem, applicationAccount);
                        }
                        else if (this.cachedItem != null && this.cachedItem.Value != null)
                        {
                            // if we have valid value and got some error from account service
                            // we extend update interval
                            this.updateInterval = new TimeSpan(0, 1, this.random.Next(30));
                            // and return existing value to clients
                            applicationAccount = this.cachedItem.Value;
                        }
                        else
                        {
                            log.WarnFormat(this.logCountGuard, "We got error response from AccountService for very first" +
                                           " request for account. AppId:{0}, ErrorCode:{1}, DebugMessage:{2}", 
                                applicationAccount.ApplicationId, applicationAccount.AccountServiceResult, applicationAccount.DebugMessage);
                        }
                        break;
                }


                ThreadPool.QueueUserWorkItem(s =>
                    {
                        Action<ApplicationAccount> calls;
                        lock (this.syncLock)
                        {
                            calls = this.callbacks;
                            this.callbacks = null;
                        }
                        Debug.Assert(calls != null, "calls != null");
                        calls(applicationAccount);
                    }
                );
            }
            #endregion
        }

        #endregion
    }
}