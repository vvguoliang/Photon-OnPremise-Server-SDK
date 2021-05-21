using Photon.Common.Authentication.Data;

namespace PhotonCloud.Authentication.CustomAuth
{
    public interface IVACustomAuthCounters
    {
        void IncrementCustomAuthQueueFullErrors();
        void IncrementCustomAuthQueueTimeouts();
        void IncrementCustomAuthHttpRequests(long ticks);
        void IncrementCustomAuthResultsData();
        void IncrementCustomAuthResultsAccepted();
        void IncrementCustomAuthResultsDenied();
        void IncrementCustomAuthHttpErrors(ClientAuthenticationType clientAuthType);
        void IncrementCustomAuthHttpTimeouts(ClientAuthenticationType clientAuthType);
        void IncrementCustomAuthErrors(ClientAuthenticationType clientAuthType);
    }
}
