// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CachedFuncBase.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the CachedFuncBase type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

//namespace Photon.VirtualApps.Master.Caching
//{
//    using System;
//    using System.Collections.Generic;
//    using System.Threading;

//    public abstract class CachedFuncBase<TArgs, TResult>
//    {
//        public readonly TimeSpan RefreshInterval;

//        public readonly TimeSpan ResultExpiration;

//        private readonly Dictionary<TArgs, CachedResult> dictionary;

//        private readonly object syncLock = new object();

//        protected CachedFuncBase(TimeSpan updateInterval, TimeSpan resultExpiration)
//        {
//            this.RefreshInterval = updateInterval;
//            this.ResultExpiration = resultExpiration;
//            this.dictionary = new Dictionary<TArgs, CachedResult>();
//        }

//        public TResult GetResult(TArgs args)
//        {
//            CachedResult cacheResult;
//            if (!this.dictionary.TryGetValue(args, out cacheResult))
//            {
//                cacheResult = this.CreateCachedResult(args);
//            }

//            return cacheResult.GetResult();
//        }

//        protected abstract TResult Invoke(TArgs args);

//        private CachedResult CreateCachedResult(TArgs args)
//        {
//            lock (this.syncLock)
//            {
//                CachedResult item;
//                if (this.dictionary.TryGetValue(args, out item))
//                {
//                    return item;
//                }

//                item = new CachedResult(this, args);
//                this.dictionary.Add(args, item);
//                return item;
//            }
//        }

//        private class CachedResult
//        {
//            private readonly object syncLock = new object();

//            private readonly CachedFuncBase<TArgs, TResult> cachedFunc;

//            private readonly TArgs args;
 
//            public CachedResult(CachedFuncBase<TArgs, TResult> cachedFunc, TArgs args)
//            {
//                this.cachedFunc = cachedFunc;
//                this.args = args;
//            }

//            private CacheItem<TResult> Result { get; set; }

//            public TResult GetResult()
//            {
//                CacheItem<TResult> cachedResult = this.Result;
//                if (cachedResult != null && cachedResult.ItemAge < this.cachedFunc.RefreshInterval)
//                {
//                    return cachedResult.Value;
//                }

//                if (Monitor.TryEnter(this.syncLock))
//                {
//                    try
//                    {
//                        TResult result = this.cachedFunc.Invoke(this.args);
//                        this.Result = new CacheItem<TResult>(result);
//                        return result;
//                    }
//                    finally
//                    {
//                        Monitor.Exit(this.syncLock);
//                    }
//                }

//                if (cachedResult == null || cachedResult.ItemAge > this.cachedFunc.ResultExpiration)
//                {
//                    try
//                    {
//                        Monitor.Enter(this.syncLock);
//                    }
//                    finally
//                    {
//                        Monitor.Exit(this.syncLock);
//                    }
//                }

//                return this.Result.Value;
//            }
//        }
//    }
//}
