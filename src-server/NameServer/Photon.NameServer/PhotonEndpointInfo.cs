
namespace Photon.NameServer
{
    using System;
    using System.Collections.Generic;

    using Photon.SocketServer;
    using Configuration;
    using Common.Authentication.Data;
    public class PhotonEndpointInfo
    {
        public PhotonEndpointInfo(Node nodeInfo)
        {
            var udpPort = nodeInfo.PortUdp > 0 ? nodeInfo.PortUdp : Settings.Default.MasterServerPortUdp;
            var tcpPort = nodeInfo.PortTcp > 0 ? nodeInfo.PortTcp : Settings.Default.MasterServerPortTcp;
            var webSocketPort = nodeInfo.PortWebSocket > 0 ? nodeInfo.PortWebSocket : Settings.Default.MasterServerPortWebSocket;
            var secureWebSocketPort = nodeInfo.PortSecureWebSocket > 0 ? nodeInfo.PortSecureWebSocket : Settings.Default.MasterServerPortSecureWebSocket;
            var httpPort = nodeInfo.PortHttp > 0 ? nodeInfo.PortHttp : Settings.Default.MasterServerPortHttp;
            var secureHttpPort = nodeInfo.PortSecureHttp > 0 ? nodeInfo.PortSecureHttp : Settings.Default.MasterServerPortSecureHttp;
            var httpPath = !string.IsNullOrEmpty(nodeInfo.HttpPath)  ? "/" + nodeInfo.HttpPath : string.IsNullOrEmpty(Settings.Default.MasterServerHttpPath) ? string.Empty : "/" + Settings.Default.MasterServerHttpPath;
            var webRTCPort = nodeInfo.PortWebRTC > 0 ? nodeInfo.PortWebRTC : Settings.Default.MasterServerPortWebRTC;

            var ipAddress = nodeInfo.IpAddress; 
            this.UdpEndPoint = string.Format("{0}:{1}", ipAddress, udpPort);
            this.TcpEndPoint = string.Format("{0}:{1}", ipAddress, tcpPort);
            this.WebRTCEndPoint = string.Format("{0}:{1}", ipAddress, webRTCPort);
            var ipAddressIPv6 = nodeInfo.IpAddressIPv6;
            if (ipAddressIPv6 != null)
            {
                this.UdpIPv6EndPoint = string.Format("[{0}]:{1}", ipAddressIPv6, udpPort);
                this.TcpIPv6EndPoint = string.Format("[{0}]:{1}", ipAddressIPv6, tcpPort);
            }

            if (!string.IsNullOrEmpty(nodeInfo.Hostname))
            {
                this.UdpHostname = string.Format("{0}:{1}", nodeInfo.Hostname, udpPort);
                this.TcpHostname = string.Format("{0}:{1}", nodeInfo.Hostname, tcpPort);
                this.WebSocketEndPoint = string.Format("ws://{0}:{1}", nodeInfo.Hostname, webSocketPort);
                this.HttpEndPoint = string.Format("http://{0}:{1}{2}", nodeInfo.Hostname, httpPort, httpPath);

                this.SecureWebSocketEndPoint = string.Format("wss://{0}:{1}", nodeInfo.Hostname, secureWebSocketPort);
                this.SecureHttpEndPoint = string.Format("https://{0}:{1}{2}", nodeInfo.Hostname, secureHttpPort, httpPath);

                if (ipAddressIPv6 != null)
                {
                    this.HttpIPv6EndPoint = this.HttpEndPoint;
                    this.WebSocketIPv6EndPoint = this.WebSocketEndPoint;
                    this.SecureWebSocketIPv6EndPoint = this.SecureWebSocketEndPoint;
                    this.SecureHttpIPv6EndPoint = this.SecureHttpEndPoint; 
                }
            }

            // internal use: 

            this.Region = nodeInfo.Region.ToLower();
        }

        //public string GetHostnameBasedEndpoint();

        public string SecureHttpIPv6EndPoint { get; set; }

        public string SecureWebSocketIPv6EndPoint { get; set; }

        public string WebSocketIPv6EndPoint { get; set; }

        public string HttpIPv6EndPoint { get; set; }

        public string TcpIPv6EndPoint { get; set; }

        public string UdpIPv6EndPoint { get; set; }

        public string UdpEndPoint { get; private set; }

        public string UdpHostname { get; private set; }

        public string TcpEndPoint { get; private set; }

        public string TcpHostname { get; private set; }

        public string WebSocketEndPoint { get; private set; }

        public string SecureWebSocketEndPoint { get; private set; }

        public string HttpEndPoint { get; private set; }

        public string SecureHttpEndPoint { get; private set; }

        public string WebRTCEndPoint { get; private set; }



        public string Region { get; private set; }

        public override string ToString()
        {
            return string.Format(
                "MasterServerConfig - Region:{0}",
                this.Region
                );
        }

        public string GetEndPoint(NetworkProtocolType networkProtocolType, int port, bool isIPv6 = false, bool useHostnames = false)
        {
            switch (networkProtocolType)
            {
                default:
                    throw new NotSupportedException("No Master server endpoint configured for network protocol " + networkProtocolType);

                case NetworkProtocolType.Udp:
                    return useHostnames ? this.UdpHostname :  (isIPv6 ? this.UdpIPv6EndPoint : this.UdpEndPoint);

                case NetworkProtocolType.Tcp:
                    return useHostnames ? this.TcpHostname :  (isIPv6 ? this.TcpIPv6EndPoint : this.TcpEndPoint);

                case NetworkProtocolType.WebSocket:
                    return isIPv6 ? this.WebSocketIPv6EndPoint : this.WebSocketEndPoint;
                    
                case NetworkProtocolType.SecureWebSocket:
                  return isIPv6 ? this.SecureWebSocketIPv6EndPoint : this.SecureWebSocketEndPoint; 

                case NetworkProtocolType.Http:
                    if (port == 443)
                    {
                        return isIPv6 ? this.SecureHttpIPv6EndPoint : this.SecureHttpEndPoint;
                    }
                    return isIPv6 ? this.HttpIPv6EndPoint : this.HttpEndPoint;

                case NetworkProtocolType.WebRTC:
                    //TODO
//                    return "192.168.78.204:7071";
                    return this.WebRTCEndPoint;
            }
        }
    }
}
