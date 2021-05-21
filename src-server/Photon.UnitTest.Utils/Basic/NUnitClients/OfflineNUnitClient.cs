using System;
using System.Threading;
using NUnit.Framework;
using Photon.SocketServer;
using Photon.SocketServer.UnitTesting;
using EventData = ExitGames.Client.Photon.EventData;
using OperationRequest = ExitGames.Client.Photon.OperationRequest;
using OperationResponse = ExitGames.Client.Photon.OperationResponse;
using ExitGames.Logging;
using System.Collections.Generic;

namespace Photon.UnitTest.Utils.Basic.NUnitClients
{
    public class OfflineNUnitClient : UnitTestClient, INUnitClient
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public OfflineNUnitClient(int defaultTimeout, ConnectPolicy policy)
            : base(defaultTimeout, policy.ClientVersion, policy.sdkId)
        {
            this.Policy = policy;
        }

        private PhotonApplicationProxy ServerAppProxy { get; set; }

        private ConnectPolicy Policy { get; set; }
        public Dictionary<long, object> OnMessageBuffer { get; set; }

        public string RemoteEndPoint
        {
            get
            {
                return this.ServerAppProxy.EndPoint.ToString();
            }
        }

        private bool enableOnMessageRecieveBuffer;
        public bool EnableOnMessageRecieveBuffer
        {
            get
            {
                return enableOnMessageRecieveBuffer;
            }
            set
            {
                if (value && OnMessageBuffer == null)
                {
                    OnMessageBuffer = new Dictionary<long, object>();
                }
                enableOnMessageRecieveBuffer = value;
            }
        }

        void INUnitClient.Connect(string serverAddress, byte[] token, object custom)
        {
            this.Policy.ConnectToServer(this, serverAddress, custom);
        }

        public override bool Connect(PhotonApplicationProxy serverAppProxy, object custom = null)
        {
            Assert.IsNotNull(serverAppProxy);

            if (base.Connect(serverAppProxy, custom))
            {
                this.ServerAppProxy = serverAppProxy;
                return true;
            }
            return false;
        }

        public bool SendRequest(OperationRequest op, bool encrypted)
        {
            if (this.Policy.UseSendDelayForOfflineTests)
            {
                Thread.Sleep(40);
            }
            var r = new Photon.SocketServer.OperationRequest
            {
                OperationCode = op.OperationCode,
                Parameters = op.Parameters
            };
            return this.SendOperationRequest(r, encrypted) == SendResult.Ok;
        }

        public new EventData WaitForEvent(int millisecodsWaitTime = ConnectPolicy.WaitTime)
        {
            Thread.Sleep(40);
            var res = base.WaitForEvent(millisecodsWaitTime);

            if (res == null)
            {
                return null;
            }

            return new EventData
            {
                Code = res.Code,
                Parameters = res.Parameters,
            };
        }

        public EventData WaitEvent(byte eventCode, int millisecodsWaitTime = ConnectPolicy.WaitTime)
        {
            Thread.Sleep(40);
            var res = this.EventQueue.DequeueIf(data => { return data.Code == eventCode;}, millisecodsWaitTime);

            if (res == null)
            {
                return null;
            }

            return new EventData
            {
                Code = res.Code,
                Parameters = res.Parameters,
            };
        }

        public new OperationResponse WaitForOperationResponse(int milliseconsWaitTime = ConnectPolicy.WaitTime)
        {
            Thread.Sleep(40);
            var res = base.WaitForOperationResponse(milliseconsWaitTime);
            if (res == null)
            {
                return null;
            }

            return new OperationResponse
            {
                DebugMessage = res.DebugMessage,
                OperationCode = res.OperationCode,
                Parameters = res.Parameters,
                ReturnCode = res.ReturnCode,
            };
        }

        public void EventQueueClear()
        {
            this.EventQueue.Clear();
        }
        public void OperationResponseQueueClear()
        {
            this.ResponseQueue.Clear();
        }

        public void InitEncryption()
        {
            this.InitializeEncyption();
        }

        public bool WaitForConnect(int timeout = ConnectPolicy.WaitTime)
        {
            Thread.Sleep(40);
            return true;
        }


        public void SendMessage(object message)
        {
            this.SendMessage(message, false);
        }

        protected override void OnMessage(object message)
        {
            if (!EnableOnMessageRecieveBuffer)
            {
                return;
            }
            OnMessageBuffer.Add(DateTime.Now.Ticks, message);
         }

        public NetworkProtocolType NetworkProtocol
        {
            get
            {
                return NetworkProtocolType.Tcp;
            }
        }

        public void SetupEncryption(Dictionary<byte, object> encryptionData)
        {
            // do nothing here
        }
    }
}
