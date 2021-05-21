// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Counter.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the Counter type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics;
using Photon.SocketServer.Diagnostics.Counters;
using Photon.SocketServer.Diagnostics.Counters.Wrappers;

namespace PhotonCloud.Authentication.AccountService.Diagnostic
{
    

    [PerfCounterCategory("Photon: Authentication")]
    public class PerfCounter : PerfCounterManagerBase<PerfCounter>
    {
        /// <summary>
        /// Dummy static ctor to iniate base static ctor
        /// </summary>
        static PerfCounter()
        {
            InitializeWithDefaults();
        }

        #region Counters
        [PerfCounter("AccountService Requests/sec", PerformanceCounterType.RateOfCountsPerSecond32)]
        protected PerformanceCounterWrapper AccountServiceRequests;
        [PerfCounter("AccountService Timeouts/sec", PerformanceCounterType.RateOfCountsPerSecond32)]
        protected PerformanceCounterWrapper AccountServiceTimeouts;
        [PerfCounter("AccountService Errors/sec", PerformanceCounterType.RateOfCountsPerSecond32)]
        protected PerformanceCounterWrapper AccountServiceErrors;
        [PerfCounter("AccountService Avg ms", PerformanceCounterType.AverageTimer32)]
        protected AverageCounterWrapper AccountServiceTime;
        [PerfCounter("BlobService Requests/sec", PerformanceCounterType.RateOfCountsPerSecond32)]
        protected PerformanceCounterWrapper BlobServiceRequests;
        [PerfCounter("BlobService Timeouts/sec", PerformanceCounterType.RateOfCountsPerSecond32)]
        protected PerformanceCounterWrapper BlobServiceTimeouts;
        [PerfCounter("BlobService Errors/sec", PerformanceCounterType.RateOfCountsPerSecond32)]
        protected PerformanceCounterWrapper BlobServiceErrors;
        [PerfCounter("BlobService Avg ms", PerformanceCounterType.AverageTimer32)]
        protected AverageCounterWrapper BlobServiceTime;
        [PerfCounter("BlobService CachMiss/sec", PerformanceCounterType.RateOfCountsPerSecond32)]
        protected PerformanceCounterWrapper BlobServiceCacheMisses;

        [PerfCounter("FallbackBlob Requests/sec", PerformanceCounterType.RateOfCountsPerSecond32)]
        protected PerformanceCounterWrapper FallbackBlobRequests;
        [PerfCounter("FallbackBlob Timeouts/sec", PerformanceCounterType.RateOfCountsPerSecond32)]
        protected PerformanceCounterWrapper FallbackBlobTimeouts;
        [PerfCounter("FallbackBlob Errors/sec", PerformanceCounterType.RateOfCountsPerSecond32)]
        protected PerformanceCounterWrapper FallbackBlobErrors;
        [PerfCounter("FallbackBlob Avg ms", PerformanceCounterType.AverageTimer32)]
        protected AverageCounterWrapper FallbackBlobTime;
        [PerfCounter("FallbackBlob CachMiss/sec", PerformanceCounterType.RateOfCountsPerSecond32)]
        protected PerformanceCounterWrapper FallbackBlobCacheMisses;
        #endregion

        public void IncrementAccountServiceRequests(long ticks)
        {
            if (!isInitialized)
            {
                return;
            }

            this.AccountServiceRequests.Increment();
            this.AccountServiceTime.Increment(ticks * 1000);

            GlobalInstance.AccountServiceRequests.Increment();
            GlobalInstance.AccountServiceTime.Increment(ticks * 1000);
        }

        public void IncrementAccountServiceTimeouts()
        {
            if (!isInitialized)
            {
                return;
            }

            this.AccountServiceTimeouts.Increment();
            GlobalInstance.AccountServiceTimeouts.Increment();
        }

        public void IncrementAccountServiceErrors()
        {
            if (!isInitialized)
            {
                return;
            }

            this.AccountServiceErrors.Increment();
            GlobalInstance.AccountServiceErrors.Increment();
        }

        public void IncrementBlobServiceRequests(long ticks)
        {
            if (!isInitialized)
            {
                return;
            }

            this.BlobServiceRequests.Increment();
            this.BlobServiceTime.Increment(ticks * 1000);

            GlobalInstance.BlobServiceRequests.Increment();
            GlobalInstance.BlobServiceTime.Increment(ticks * 1000);
        }

        public void IncrementBlobServiceTimeouts()
        {
            if (!isInitialized)
            {
                return;
            }

            this.BlobServiceTimeouts.Increment();
            GlobalInstance.BlobServiceTimeouts.Increment();
        }

        public void IncrementBlobServiceErrors()
        {
            if (!isInitialized)
            {
                return;
            }

            this.BlobServiceErrors.Increment();
            GlobalInstance.BlobServiceErrors.Increment();
        }

        public void IncrementBlobServiceCacheMisses()
        {
            if (!isInitialized)
            {
                return;
            }

            this.BlobServiceCacheMisses.Increment();
            GlobalInstance.BlobServiceCacheMisses.Increment();
        }

        public void IncrementFallbackBlobRequests(long ticks)
        {
            if (!isInitialized)
            {
                return;
            }

            this.FallbackBlobRequests.Increment();
            this.FallbackBlobTime.Increment(ticks * 1000);

            GlobalInstance.FallbackBlobRequests.Increment();
            GlobalInstance.FallbackBlobTime.Increment(ticks * 1000);

        }

        public void IncrementFallbackBlobCacheMisses()
        {
            if (!isInitialized)
            {
                return;
            }

            this.FallbackBlobCacheMisses.Increment();
            GlobalInstance.FallbackBlobCacheMisses.Increment();
        }

        public void IncrementFallbackBlobTimeouts()
        {
            if (!isInitialized)
            {
                return;
            }

            this.FallbackBlobTimeouts.Increment();
            GlobalInstance.FallbackBlobTimeouts.Increment();
        }

        public void IncrementFallbackBlobErrors()
        {
            if (!isInitialized)
            {
                return;
            }

            this.FallbackBlobErrors.Increment();
            GlobalInstance.FallbackBlobErrors.Increment();
        }
    }
}
