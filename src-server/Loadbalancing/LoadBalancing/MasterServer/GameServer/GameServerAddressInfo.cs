using System.Net;
using System.Net.Sockets;
using ExitGames.Logging;
using Photon.LoadBalancing.ServerToServer.Operations;

namespace Photon.LoadBalancing.MasterServer.GameServer
{
    public class GameServerAddressInfo
    {
        #region Properties

        public string Address { get; private set; }

        public string AddressIPv6 { get; private set; }

        public string Hostname { get; private set; }

        // IPv4
        public string TcpAddress { get; private set; }

        public string UdpAddress { get; private set; }

        public string WebSocketAddress { get; private set; }

        public string HttpAddress { get; private set; }

        public string WebRTCAddress { get; private set; }
  
        // IPv6
        public string TcpAddressIPv6 { get; private set; }

        public string UdpAddressIPv6 { get; private set; }

        public string WebSocketAddressIPv6 { get; private set; }

        public string HttpAddressIPv6 { get; private set; }

        // Hostname
        public string TcpHostname { get; private set; }

        public string UdpHostname { get; private set; }

        public string WebSocketHostname { get; private set; }

        public string HttpHostname { get; private set; }

        public string SecureWebSocketHostname { get; private set; }

        public string SecureHttpHostname { get; private set; }

        #endregion

        public static GameServerAddressInfo CreateAddressInfo(IRegisterGameServer registerRequest, ILogger log)
        {
            var result = new GameServerAddressInfo
            {
                Address = registerRequest.GameServerAddress
            };

            if (registerRequest.GameServerAddressIPv6 != null
                && IPAddress.Parse(registerRequest.GameServerAddressIPv6).AddressFamily == AddressFamily.InterNetworkV6)
            {
                result.AddressIPv6 = string.Format("[{0}]", IPAddress.Parse(registerRequest.GameServerAddressIPv6));
            }
            result.Hostname = registerRequest.GameServerHostName;

            if (registerRequest.UdpPort.HasValue)
            {
                result.UdpAddress = string.IsNullOrEmpty(result.Address) ? null : string.Format("{0}:{1}", result.Address, registerRequest.UdpPort);
                result.UdpAddressIPv6 = string.IsNullOrEmpty(result.AddressIPv6) ? null : string.Format("{0}:{1}", result.AddressIPv6, registerRequest.UdpPort);
                result.UdpHostname = string.IsNullOrEmpty(result.Hostname) ? null : string.Format("{0}:{1}", result.Hostname, registerRequest.UdpPort);
            }

            if (registerRequest.TcpPort.HasValue)
            {
                result.TcpAddress = string.IsNullOrEmpty(result.Address) ? null : string.Format("{0}:{1}", result.Address, registerRequest.TcpPort);
                result.TcpAddressIPv6 = string.IsNullOrEmpty(result.AddressIPv6) ? null : string.Format("{0}:{1}", result.AddressIPv6, registerRequest.TcpPort);
                result.TcpHostname = string.IsNullOrEmpty(result.Hostname) ? null : string.Format("{0}:{1}", result.Hostname, registerRequest.TcpPort);
            }

            if (registerRequest.WebSocketPort.HasValue && registerRequest.WebSocketPort != 0)
            {
                result.WebSocketAddress = string.IsNullOrEmpty(result.Address)
                    ? null
                    : string.Format("ws://{0}:{1}", result.Address, registerRequest.WebSocketPort);

                result.WebSocketAddressIPv6 = string.IsNullOrEmpty(result.AddressIPv6)
                    ? null
                    : string.Format("ws://{0}:{1}", result.AddressIPv6, registerRequest.WebSocketPort);

                result.WebSocketHostname = string.IsNullOrEmpty(result.Hostname)
                    ? null
                    : string.Format("ws://{0}:{1}", result.Hostname, registerRequest.WebSocketPort);
            }

            if (registerRequest.HttpPort.HasValue && registerRequest.HttpPort != 0)
            {
                result.HttpAddress = string.IsNullOrEmpty(result.Address)
                    ? null
                    : string.Format("http://{0}:{1}{2}", result.Address, registerRequest.HttpPort, registerRequest.HttpPath);

                result.HttpAddressIPv6 = string.IsNullOrEmpty(result.AddressIPv6)
                    ? null
                    : string.Format("http://{0}:{1}{2}", result.AddressIPv6, registerRequest.HttpPort, registerRequest.HttpPath);

                result.HttpHostname = string.IsNullOrEmpty(result.Hostname)
                    ? null
                    : string.Format("http://{0}:{1}{2}", result.Hostname, registerRequest.HttpPort, registerRequest.HttpPath);
            }

            if (registerRequest.WebRTCPort.HasValue && registerRequest.WebRTCPort != 0)
            {
                result.WebRTCAddress = string.IsNullOrEmpty(result.Address)
                    ? null
                    : string.Format("{0}:{1}", result.Address, registerRequest.WebRTCPort);
            }

            // HTTP & WebSockets require a proper domain name (especially for certificate validation on secure Websocket & HTTPS connections): 
            if (string.IsNullOrEmpty(result.Hostname))
            {
                log.WarnFormat("HTTPs & Secure WebSockets not supported. GameServer {0} does not have a public hostname.", result.Address);
            }
            else
            {
                if (registerRequest.SecureWebSocketPort.HasValue && registerRequest.SecureWebSocketPort != 0)
                {
                    result.SecureWebSocketHostname = string.Format("wss://{0}:{1}", result.Hostname, registerRequest.SecureWebSocketPort);
                }

                if (registerRequest.SecureHttpPort.HasValue && registerRequest.SecureHttpPort != 0)
                {
                    result.SecureHttpHostname = string.Format("https://{0}:{1}{2}", result.Hostname, registerRequest.SecureHttpPort, registerRequest.HttpPath);
                }
            }
            return result;
        }
    }
}
