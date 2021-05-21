using ExitGames.Logging;
using Photon.Common;
using Photon.Common.Authentication;
using Photon.SocketServer;
using Photon.SocketServer.Security;

namespace Photon.LoadBalancing.Common
{
    /// <summary>
    /// Checkes that client used connection with required properties
    /// </summary>
    class ConnectionRequirementsChecker
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public static bool Check(PeerBase peer, string appId, AuthenticationToken token, bool authOnceUsed)
        {
            return CheckSecureConnectionRequirement(peer, appId, token, authOnceUsed);
        }

        private static bool CheckSecureConnectionRequirement(PeerBase peer, string appId, AuthenticationToken token, bool authOnceUsed)
        {
            if (!CommonSettings.Default.RequireSecureConnection)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"Secure Connection Check: Account does not require connection to be secure. appId:{appId}");
                }
                return true;
            }

            if (log.IsDebugEnabled)
            {
                log.Debug($"Secure Connection Check: Account requires connection to be secure. appId:{appId}");
            }
            if (peer.NetworkProtocol == NetworkProtocolType.SecureWebSocket)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"Secure Connection Check passed. Peer uses SecureWebSocket. appId:{appId}");
                }
                return true;
            }

            if (peer.NetworkProtocol == NetworkProtocolType.Udp && authOnceUsed)
            {
                var authToken = token;
                if (authToken.EncryptionData != null
                    && authToken.EncryptionData.TryGetValue(EncryptionDataParameters.EncryptionMode, out var encryptionMode)
                    && (byte)encryptionMode == (byte)EncryptionModes.DatagramEncyption)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"Secure Connection Check passed. Peer uses full encryption over udp. appId:{appId}");
                    }
                    return true;
                }
            }

            if (log.IsDebugEnabled)
            {
                log.Debug($"Secure Connection Check failed. appId:{appId}, Connection Type:{peer.NetworkProtocol}, AuthOnceUsed:{authOnceUsed}");
            }

            peer.SendOperationResponseAndDisconnect(new OperationResponse((byte) (authOnceUsed ? Operations.OperationCode.AuthOnce : Operations.OperationCode.Authenticate))
            {
                ReturnCode = (int)ErrorCode.SecureConnectionRequired,
                DebugMessage = LBErrorMessages.SecureConnectionRequired,
            }, new SendParameters());

            return false;
        }
    }
}
