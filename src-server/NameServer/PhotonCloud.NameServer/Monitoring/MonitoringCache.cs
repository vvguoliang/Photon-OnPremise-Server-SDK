// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AuthenticateCache.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the AuthenticateCache type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ExitGames.Concurrency.Fibers;
using ExitGames.Logging;
using ExitGames.Threading;
using Photon.Common.Authentication;
using Photon.SocketServer;
using Photon.SocketServer.Diagnostics;
using Photon.VirtualApps.Master.Caching;
using PhotonCloud.Authentication;
using PhotonCloud.Authentication.AccountService;

namespace PhotonCloud.NameServer.Monitoring
{
//    using Settings = PhotonCloud.NameServer.Settings;

    public class MonitoringCache
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly MonitoringService monitoringService;

        private readonly Dictionary<string, CachedResult> dictionary;

        private readonly TimeSpan refreshInterval;

//        private readonly bool allowOnFailure;

//        protected readonly HashSet<PeerBase> Subscribers = new HashSet<PeerBase>();

        #endregion

        #region Constructors and Destructors

        protected MonitoringCache(TimeSpan updateInterval, MonitoringService monitoringService/*, bool allowOnFailure*/)
        {
            this.refreshInterval = updateInterval;
            this.dictionary = new Dictionary<string, CachedResult>();
            this.monitoringService = monitoringService;
//            this.allowOnFailure = allowOnFailure;

            if (log.IsInfoEnabled)
            {
                log.InfoFormat("MonitoringCache created: updateInterval={0}", updateInterval);
            }
        }

        #endregion

        #region Public Methods

        public static MonitoringCache CreateCache(MonitoringService monitoringService/*, bool allowOnFailure*/)
        {

            TimeSpan updateInterval;
            if (Settings.Default.MonitoringCacheUpdateInterval <= 0)
            {
                log.WarnFormat("Invalid update interval {0} specified for MonitoringCache. Using default value.");
                updateInterval = TimeSpan.FromSeconds(60);
            }
            else
            {
                updateInterval = TimeSpan.FromSeconds(Settings.Default.MonitoringCacheUpdateInterval);
            }

            return new MonitoringCache(updateInterval, monitoringService/*, allowOnFailure*/);
        }

        //        public void AddSubscriber(PeerBase subscriber)
        //        {
        //            using (Lock.TryEnter(this.Subscribers, 10000))
        //            {
        //                this.Subscribers.Add(subscriber);
        //            }
        //        }


        public bool GetMonitoringResultFromCache(string applicationId, Action<MonitoringResult, object> callback, object state)
        {
            //TODO format appId just in case
            applicationId = applicationId.Trim();
            if (string.IsNullOrEmpty(applicationId))
            {
//                callback(new MonitoringResult(null, MonitoringServiceResult.Error, null, "Empty AppId"), state);
                return false;
            }

            string appId;
            if (!this.monitoringService.FormatApplicationId(applicationId, out appId))
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Invalid application id format: {0}", applicationId);
                }

//                callback(new MonitoringResult(null, MonitoringServiceResult.Error, null, "Invalid AppId format"), state);
                return false;
            }

            CachedResult result;
            using (Lock.TryEnter(this.dictionary, 10000))
            {
                if (!this.dictionary.TryGetValue(appId, out result))
                {
//                    callback(new MonitoringResult(appId, MonitoringServiceResult.NotFound, null), state);
                    return false;
                }
            }
            
            result.GetResult(callback, state);
            return true;
        }

        public void GetMonitoringResult(string applicationId, string servernames, Action<MonitoringResult, object> callback, object state)
        {
            if (callback == null)
            {
                throw new ArgumentNullException("callback");
            }

//            if (fiber == null)
//            {
//                throw new ArgumentNullException("fiber");
//            }

//            var tmp = callback;
//            callback = account => fiber.Enqueue(() => tmp(account, state));

            if (applicationId == null)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("Application id is null.");
                }

                //TODO? add error+message to MonitoringResult?
//                callback(new ApplicationAccount(string.Empty, AccountServiceResult.Error, false, ErrorMessages.AppIdMissing));
                callback(new MonitoringResult(null, MonitoringServiceResult.Error, null, "No AppId"), state);
                return;
            }

            applicationId = applicationId.Trim();
            if (string.IsNullOrEmpty(applicationId))
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("app id is Empty");
                }

//                callback(new ApplicationAccount(string.Empty, AccountServiceResult.Error, false, ErrorMessages.EmptyAppId));
                callback(new MonitoringResult(null, MonitoringServiceResult.Error, null, "Empty AppId"), state);
                return;
            }

            string appId;
            if (!this.monitoringService.FormatApplicationId(applicationId, out appId))
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Invalid application id format: {0}", applicationId);
                }

//                callback(new ApplicationAccount(string.Empty, AccountServiceResult.Error, false, ErrorMessages.InvalidAppIdFormat));
                callback(new MonitoringResult(null, MonitoringServiceResult.Error, null, "Invalid AppId format"), state);
                return;
            }

            CachedResult result;
            using (Lock.TryEnter(this.dictionary, 10000))
            {
                if (!this.dictionary.TryGetValue(appId, out result))
                {
                    log.DebugFormat("Create cached monitoring result for app: {0}", appId);
                    result = new CachedResult(this, appId, servernames, this.refreshInterval);
                    this.dictionary.Add(appId, result);
                }
            }

            result.GetResult(callback, state);
        }

//        public Hashtable GetAll()
//        {
//            var hashtable = new Hashtable(this.dictionary.Count);
//
//            using (Lock.TryEnter(this.dictionary, 10000))
//            {
//                foreach (var item in this.dictionary.Values)
//                {
//                    CacheItem<MonitoringResult> cachedItem = item.CachedItem;
//                    if (cachedItem != null && cachedItem.Value != null)
//                    {
//                        var authResult = cachedItem.Value;
//
//                        var isClientAuthenticationRequired = authResult.IsClientAuthenticationEnabled && !authResult.IsAnonymousAccessAllowed;
//                        var hasExternalApi = authResult.HasExternalApi;
//
//                        hashtable.Add(
//                            item.ApplicationId,
//                            new object[] {
//                                authResult.IsAuthenticated,
//                                isClientAuthenticationRequired,
//                                authResult.IsClientAuthenticationEnabled,
//                                hasExternalApi});
//                    }
//                }
//            }
//
//            return hashtable;
//        }

//        public void RemoveSubscriber(PeerBase subscriber)
//        {
//            using (Lock.TryEnter(this.Subscribers, 10000))
//            {
//                this.Subscribers.Remove(subscriber);
//            }
//        }

        #endregion

        #region Methods

//        protected virtual void OnAuthenticateResultUpdated(string appId, ApplicationAccount appAccount)
//        {
//        }

        private class CachedResult
        {
            #region Constants and Fields

            public readonly string ApplicationId;

            public readonly string Servernames;

            private readonly MonitoringCache cache;

            private readonly object syncLock = new object();

            private TimeSpan updateInterval;

            private readonly TimeSpan initialUpdateInterval;

            private CacheItem<MonitoringResult> cachedItem;

            private event Action<MonitoringResult, object> callbacks;

            private readonly Random random = new Random();

            private readonly LogCountGuard logCountGuard = new LogCountGuard(new TimeSpan(0, 1, 0));
            #endregion

            #region Constructors and Destructors

            public CachedResult(MonitoringCache cache, string applicationId, string servernames, TimeSpan updateInterval)
            {
                this.cache = cache;
                this.ApplicationId = applicationId;
                this.Servernames = servernames;
                this.updateInterval = updateInterval;
                this.initialUpdateInterval = updateInterval;
            }

            #endregion

            #region Properties

            public CacheItem<MonitoringResult> CachedItem
            {
                get
                {
                    return this.cachedItem;
                }
            }

            private TimeSpan UpdateInterval { get { return this.updateInterval; } }
            #endregion

            #region Public Methods

            public void GetResult(Action<MonitoringResult, object> callback, object state)
            {
                // check if there is already a cache result and the result is still valid
                var cachedResult = this.cachedItem;
                if (cachedResult != null && cachedResult.ItemAge < this.UpdateInterval)
                {
                    // the cached result is still valid and can be returned
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("The cached result is still valid and can be returned. AppId: {0}", cachedResult.Value.ApplicationId);
                    }

                    callback(cachedResult.Value, state);
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
                    callback(cachedResult.Value, state);
                    return;
                }

                // requesting data from account service
                this.cache.monitoringService.VerifyMonitoringResult(this.ApplicationId, this.Servernames, this.OnGetMonitoringResult, state);
            }

            #endregion

            #region Methods

            private void SetCacheItem(CacheItem<MonitoringResult> currentItem, MonitoringResult result)
            {
                var newCachedItem = new CacheItem<MonitoringResult>(result);
                var oldItem = Interlocked.CompareExchange(ref this.cachedItem, newCachedItem, currentItem);
                if (oldItem == currentItem)
                {
//                    this.cache.OnAuthenticateResultUpdated(this.ApplicationId, result);

                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Updated cached monitoring result for {0}: {1}", this.ApplicationId, result.Status);
                    }
                }
            }

            private void OnGetMonitoringResult(MonitoringResult result, object state)
            {
//                if (log.IsDebugEnabled)
//                {
//                    log.DebugFormat("Got monitoring result: appId={0}", this.ApplicationId);
//                }

                if (log.IsDebugEnabled)
                {
                    log.DebugFormat(
                        "MonitoringResult: appId={0}, result={1}, status={2}",
                        this.ApplicationId,
                        result.MonitoringServiceResult,
                        result.Status);
                }

                // store in cache EITHER if the authentication call succeeded OR 
                // if we allow "failed" auth calls. 
                switch (result.MonitoringServiceResult)
                {
                    case MonitoringServiceResult.Ok:
                        this.SetCacheItem(this.cachedItem, result);
                        this.updateInterval = this.initialUpdateInterval;
                        break;

                    default:
                        if (result.MonitoringServiceResult == MonitoringServiceResult.NotFound)
                        {
                            log.DebugFormat("NOT found. appId:'{0}'", this.ApplicationId);
                        }

//                        if (this.cache.allowOnFailure)
//                        {
//                            this.SetCacheItem(this.cachedItem, result);
//                        }
//                        else 
                        if (this.cachedItem != null && this.cachedItem.Value != null)
                        {
                            // if we have valid value and got some error from account service
                            // we extend update interval
                            this.updateInterval = new TimeSpan(0, 1, this.random.Next(30));
                            // and return existing value to clients
                            result = this.cachedItem.Value;
                        }
                        else
                        {
                            log.WarnFormat(this.logCountGuard, "We got error response from MonitoringService for very first" +
                                           " request. AppId:{0}, ErrorCode:{1}, DebugMessage:{2}",
                                result.ApplicationId, result.MonitoringServiceResult, result.DebugMessage);
                        }
                        break;
                }


                ThreadPool.QueueUserWorkItem(s =>
                {
                    Action<MonitoringResult, object> calls;
                    lock (this.syncLock)
                    {
                        calls = this.callbacks;
                        this.callbacks = null;
                    }
                    Debug.Assert(calls != null, "calls != null");
                    calls(result, state);
                }
                );
            }
            #endregion
        }

        #endregion
    }
}