using Photon.Cloud.Common.Diagnostic.HealthCheck;
using Photon.SocketServer.Net;

namespace PhotonCloud.Authentication.AccountService.Health
{
    public class AccountServiceHealthController : IHealthController
    {
        private const string SubsystemName = "AccountService";

        private readonly HttpRequestQueue blobHttpQueue;
        private readonly HttpRequestQueue accountServiceHttpQueue;
        private SubsystemState healthState = SubsystemState.MakeOK(SubsystemName);

        private bool initialCheck = true;

        public AccountServiceHealthController(HttpRequestQueue blobHttpQueue, HttpRequestQueue accountServiceHttpQueue)
        {
            this.blobHttpQueue = blobHttpQueue;
            this.accountServiceHttpQueue = accountServiceHttpQueue;
        }

        internal void OnGetResponseFromBlobstore(AccountService.AsyncRequestState asyncRequestState)
        {
            
        }

        internal void OnGetResponseFromAccountService(AccountService.AsyncRequestState asyncRequestState)
        {
            switch (asyncRequestState.HttpRequestQueueResultCode)
            {
                case HttpRequestQueueResultCode.QueueTimeout:
                    break;
                case HttpRequestQueueResultCode.Offline:
                    break;
                case HttpRequestQueueResultCode.QueueFull:
                    break;
                default:
                {
                    if (this.healthState.HealthStatus != HealthStatus.Ok)
                    {
                        this.healthState = SubsystemState.MakeOK(SubsystemName);
                    }
                    break;
                }
            }
        }

        public SubsystemState GetHealth()
        {
            if (this.initialCheck)
            {
                if (this.accountServiceHttpQueue.QueueState == HttpRequestQueueState.Connecting
                    && this.blobHttpQueue.QueueState == HttpRequestQueueState.Connecting)
                {
                    return this.healthState;
                }

                this.initialCheck = false;
            }

            if (this.accountServiceHttpQueue.QueueState != HttpRequestQueueState.Running
                && this.blobHttpQueue.QueueState != HttpRequestQueueState.Running)
            {
                return new SubsystemState
                {
                    Description = "AccountService is not reachable",
                    Subsystem = SubsystemName,
                    HealthStatus = HealthStatus.Warn,
                };
            }
            return this.healthState;
        }
    }
}
