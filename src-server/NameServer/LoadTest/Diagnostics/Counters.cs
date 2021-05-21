// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Counters.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   The counters.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LoadTest.Diagnostics
{
    using ExitGames.Diagnostics.Counter;

    /// <summary>
    /// The counters.
    /// </summary>
    public static class Counters
    {
        /// <summary>
        /// The connected clients.
        /// </summary>
        public static readonly NumericCounter TotalClients = new NumericCounter();

        /// <summary>
        /// The received events.
        /// </summary>
        public static readonly CountsPerSecondCounter RequestsReceived = new CountsPerSecondCounter();

        /// <summary>
        /// The round trip time.
        /// </summary>
        public static readonly AverageCounter RoundTripTime = new AverageCounter();

        /// <summary>
        /// The round trip time.
        /// </summary>
        public static readonly AverageCounter ConnectionTime = new AverageCounter();

        /// <summary>
        /// The send operations.
        /// </summary>
        public static readonly CountsPerSecondCounter RequestsSent = new CountsPerSecondCounter();

        /// <summary>
        /// The number of flush invoking operations sent per second.
        /// </summary>
        public static readonly CountsPerSecondCounter SuccessResponses = new CountsPerSecondCounter();

        /// <summary>
        /// The number of flush invoking operations sent per second.
        /// </summary>
        public static readonly CountsPerSecondCounter FirstMethodResponses = new CountsPerSecondCounter();

        /// <summary>
        /// The number of flush invoking operations sent per second.
        /// </summary>
        public static readonly CountsPerSecondCounter FailedResponses = new CountsPerSecondCounter();

        /// <summary>
        /// The round trip variance.
        /// </summary>
        public static readonly AverageCounter RoundTripTimeVariance = new AverageCounter();

        /// <summary>
        /// The round trip variance.
        /// </summary>
        public static readonly CountsPerSecondCounter ConnectFailures = new CountsPerSecondCounter();

    }
}