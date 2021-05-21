// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Counter.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the Counter type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using ExitGames.Diagnostics.Counter;
using ExitGames.Diagnostics.Monitoring;
using ExitGames.Logging;

namespace PhotonCloud.Authentication.AccountService.Diagnostic
{
    //TODO Check. Looks like this class has duplicate functionality with new generic counter manager
    /// <summary>
    /// Counter on application level
    /// </summary>
    public static class Counter
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private static readonly object syncRoot = new object();

        private static PerfCounter performanceCounter;

        private static string perfCounterInstanceName;

        public static void InitializePerformanceCounter(string instanceName)
        {
            lock (syncRoot)
            {
                if (performanceCounter != null)
                {
                    throw new InvalidOperationException("PerformanceCounter have allready been initialized.");
                }

                try
                {
                    PerfCounter.Initialize();
                    perfCounterInstanceName = instanceName;
                    performanceCounter = PerfCounter.GetInstance(instanceName);
                }
                catch(Exception)
                {
                    performanceCounter = null;
                    throw;
                }
            }
        }

        public static void IncrementAccountServiceRequests(long ticks)
        {
            accountServiceRequests.Increment();

            try
            { 
            var perfCounter = performanceCounter;
            if (perfCounter != null)
            {
                perfCounter.IncrementAccountServiceRequests(ticks);
            }
            }
            catch (Exception ex)
            {
                HandlePerformanceCounterUpdateException(ex);
            }
        }

        public static void IncrementAccountServiceTimeouts()
        {
            accountServiceTimeout.Increment();

            try
            {
                var perfCounter = performanceCounter;
                if (perfCounter != null)
                {
                    perfCounter.IncrementAccountServiceTimeouts();
                }
            }
            catch (Exception ex)
            {
                HandlePerformanceCounterUpdateException(ex);
            }
        }

        public static void IncrementAccountServiceServiceErrors()
        {
            try
            { 
            var perfCounter = performanceCounter;
            if (perfCounter != null)
            {
                perfCounter.IncrementAccountServiceErrors();
            }
            }
            catch (Exception ex)
            {
                HandlePerformanceCounterUpdateException(ex);
            }
        }

        public static void IncrementBlobServiceRequests(long ticks)
        {
            blobServiceRequests.Increment();

            try
            { 
            var perfCounter = performanceCounter;
            if (perfCounter != null)
            {
                perfCounter.IncrementBlobServiceRequests(ticks);
            }
            }
            catch (Exception ex)
            {
                HandlePerformanceCounterUpdateException(ex);
            }
        }

        public static void IncrementBlobServiceCacheMisses()
        {
            try
            {
                var perfCounter = performanceCounter;
                if (perfCounter != null)
                {
                    perfCounter.IncrementBlobServiceCacheMisses();
                }
            }
            catch (Exception ex)
            {
                HandlePerformanceCounterUpdateException(ex);
            }
        }

        public static void IncrementBlobServiceTimeouts()
        {
            blobServiceTimeout.Increment();

            try
            { 
            var perfCounter = performanceCounter;
            if (perfCounter != null)
            {
                perfCounter.IncrementBlobServiceTimeouts();
            }
            }
            catch (Exception ex)
            {
                HandlePerformanceCounterUpdateException(ex);
            }
        }

        public static void IncrementBlobServiceErrors()
        {
            try
            {
                var perfCounter = performanceCounter;
                if (perfCounter != null)
                {
                    perfCounter.IncrementBlobServiceErrors();
                }
            }
            catch (Exception ex)
            {
                HandlePerformanceCounterUpdateException(ex);
            }
        }

        public static void IncrementFallbackBlobServiceRequests(long ticks)
        {
            fallbackBlobServiceRequests.Increment();

            try
            { 
                var perfCounter = performanceCounter;
                if (perfCounter != null)
                {
                    perfCounter.IncrementFallbackBlobRequests(ticks);
                }
            }
            catch (Exception ex)
            {
                HandlePerformanceCounterUpdateException(ex);
            }
        }

        public static void IncrementFallbackBlobServiceCacheMisses()
        {
            try
            {
                var perfCounter = performanceCounter;
                if (perfCounter != null)
                {
                    perfCounter.IncrementFallbackBlobCacheMisses();
                }
            }
            catch (Exception ex)
            {
                HandlePerformanceCounterUpdateException(ex);
            }
        }

        public static void IncrementFallbackBlobServiceTimeouts()
        {
            fallbackBlobServiceTimeout.Increment();

            try
            { 
                var perfCounter = performanceCounter;
                if (perfCounter != null)
                {
                    perfCounter.IncrementFallbackBlobTimeouts();
                }
            }
            catch (Exception ex)
            {
                HandlePerformanceCounterUpdateException(ex);
            }
        }

        public static void IncrementFallbackBlobServiceErrors()
        {
            try
            {
                var perfCounter = performanceCounter;
                if (perfCounter != null)
                {
                    perfCounter.IncrementFallbackBlobErrors();
                }
            }
            catch (Exception ex)
            {
                HandlePerformanceCounterUpdateException(ex);
            }
        }

        private static void HandlePerformanceCounterUpdateException(Exception ex)
        {
            lock(syncRoot)
            {
                performanceCounter = null;
            }

            log.Error(string.Format("Exception during performance counter update. Exception Msg:{0}", ex.Message), ex);
        }

        /// <summary>
        /// Number of authentication requests.
        /// </summary>
        [PublishCounter("AccountServiceRequests")]
        private static readonly AverageCounter accountServiceRequests = new AverageCounter("AccountServiceRequests");

        /// <summary>
        /// Number of authentication timeouts.
        /// </summary>
        [PublishCounter("AccountServiceTimeout")]
        private static readonly AverageCounter accountServiceTimeout = new AverageCounter("AccountServiceTimeout");

        /// <summary>
        /// Number of blob storage requests. 
        /// </summary>
        [PublishCounter("BlobServiceRequests")]
        private static readonly AverageCounter blobServiceRequests = new AverageCounter("BlobServiceRequests");

        /// <summary>
        /// Number of blob storage request timeouts. 
        /// </summary>
        [PublishCounter("BlobServiceTimeout")]
        private static readonly AverageCounter blobServiceTimeout = new AverageCounter("BlobServiceTimeout");

        /// <summary>
        /// Number of blob storage requests. 
        /// </summary>
        [PublishCounter("FallbackBlobServiceRequests")]
        private static readonly AverageCounter fallbackBlobServiceRequests = new AverageCounter("FallbackBlobServiceRequests");

        /// <summary>
        /// Number of blob storage request timeouts. 
        /// </summary>
        [PublishCounter("FallbackBlobServiceTimeout")]
        private static readonly AverageCounter fallbackBlobServiceTimeout = new AverageCounter("FallbackBlobServiceTimeout");

    }
}
