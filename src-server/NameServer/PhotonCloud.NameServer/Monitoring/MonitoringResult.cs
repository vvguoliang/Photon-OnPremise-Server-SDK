using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotonCloud.NameServer.Monitoring
{
    public class MonitoringResult
    {
        //TODO what do we need
        public string ApplicationId { get; set; }

        public MonitoringServiceResult MonitoringServiceResult { get; set; }

        public string Status { get; set; }

        public string DebugMessage { get; set; }

        public MonitoringResult(string applicationId, MonitoringServiceResult result, string status, string message = null)
        {
            this.ApplicationId = applicationId;
            this.MonitoringServiceResult = result;
            this.Status = status;
            this.DebugMessage = message;
        }
    }

    public enum MonitoringServiceResult
    {
        Ok,
        NotFound,
        Timeout,
        Error
    }
}
