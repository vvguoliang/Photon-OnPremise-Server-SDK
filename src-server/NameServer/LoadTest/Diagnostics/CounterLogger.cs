// -----------------------------------------------------------------------
// <copyright file="CounterLogger.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace LoadTest.Diagnostics
{
    using ExitGames.Logging;

    public class CounterLogger
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The print counter.
        /// </summary>
        public static void PrintCounter()
        {
            if (log.IsInfoEnabled)
            {
                log.InfoFormat(
                    "clients: {0}, rq: {1:F2}, r-rq: {2:F2}, rtt: {3:F2}, r-1meth: {8:F2}, r-succ:{4:F2}, r-fail:{5:F2}, conn-time: {6:F2}, conn-fail:{7:F2}",
                    Counters.TotalClients.GetNextValue(),
                    Counters.RequestsSent.GetNextValue(),
                    Counters.RequestsReceived.GetNextValue(),
                    Counters.RoundTripTime.GetNextValue(),
                    Counters.SuccessResponses.GetNextValue(),
                    Counters.FailedResponses.GetNextValue(),
                    Counters.ConnectionTime.GetNextValue(),
                    Counters.ConnectFailures.GetNextValue(),
                    Counters.FirstMethodResponses.GetNextValue()
                );
            }
            else
            {
                // reset average counters
                Counters.TotalClients.GetNextValue();
                Counters.RequestsReceived.GetNextValue();
                Counters.RequestsSent.GetNextValue();
                Counters.RoundTripTime.GetNextValue();
                Counters.RoundTripTimeVariance.GetNextValue();
                Counters.SuccessResponses.GetNextValue();
            }
        }
    }
}
