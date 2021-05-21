// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RedirectedClientPeer.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the RedirectedClientPeer type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using Photon.Common;
using Photon.Common.Misc;
using Photon.LoadBalancing.MasterServer;
using Photon.Common.Tools;
using Photon.SocketServer;
using Photon.LoadBalancing.ServerToServer.Operations;

namespace Photon.LoadBalancing.MasterServer
{
    using System.Net;
    using Photon.LoadBalancing.Operations;
  
    using PhotonHostRuntimeInterfaces;
    using Photon.LoadBalancing.Handler;
    using System.Collections.Generic;

    public class RedirectedClientPeer : ClientPeer
    {
        #region Constructors and Destructors

        public string username;
        public string password;

        public RedirectedClientPeer(InitRequest initRequest)
            : base(initRequest)
        {
        }

        #endregion

        #region Methods

        protected override void OnDisconnect(DisconnectReason reasonCode, string reasonDetail)
        {
            MasterApplication.log.Info("客户端断开连接");
            MasterApplication.Instance.peerList.Remove(this);//把当前对象移除
        }

        protected override void OnOperationRequest(OperationRequest operationRequest, SendParameters sendParameters)
        {
            Dictionary<byte, object> dict = operationRequest.Parameters;
            foreach (object value in dict.Values)
            {
                MasterApplication.log.Info("============RedirectedClientPeer==========:" + value.ToString());
            }

            var contract = new RedirectRepeatResponse();

            const byte masterNodeId = 1;

            // TODO: don't lookup for every operation!
            IPAddress publicIpAddress = PublicIPAddressReader.ParsePublicIpAddress(MasterServerSettings.Default.PublicIPAddress);
            switch (this.NetworkProtocol)
            {
                case NetworkProtocolType.Tcp:
                    contract.Address =
                        new IPEndPoint(
                            publicIpAddress, MasterServerSettings.Default.MasterRelayPortTcp + masterNodeId - 1).ToString
                            ();
                    break;
                case NetworkProtocolType.WebSocket:
                    contract.Address =
                        new IPEndPoint(
                            publicIpAddress, MasterServerSettings.Default.MasterRelayPortWebSocket + masterNodeId - 1).
                            ToString();
                    break;
                case NetworkProtocolType.Udp:
                    // no redirect through relay ports for UDP... how to handle? 
                    contract.Address =
                        new IPEndPoint(
                            publicIpAddress, MasterServerSettings.Default.MasterRelayPortUdp + masterNodeId - 1).ToString
                            ();
                    break;
            }


            var response = new OperationResponse(operationRequest.OperationCode, contract)
            {
                ReturnCode = (short) ErrorCode.RedirectRepeat,
                DebugMessage = "redirect"
            };

            BaseHandler handler = DictTools.GetValue<ServerToServer.Operations.OperationCode, BaseHandler>(MasterApplication.Instance.Handlerict, (ServerToServer.Operations.OperationCode)operationRequest.OperationCode);
            if (handler != null)
            {
                handler.OnOperationRequest(operationRequest, sendParameters, this);
            }
            else
            {
                BaseHandler defaultHandler = DictTools.GetValue<ServerToServer.Operations.OperationCode, BaseHandler>(MasterApplication.Instance.Handlerict,ServerToServer.Operations.OperationCode.Default);
                defaultHandler.OnOperationRequest(operationRequest, sendParameters, this);
            }

            this.SendOperationResponse(response, sendParameters);
        }

        #endregion
    }
}