using Photon.SocketServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Photon.LoadBalancing.MasterServer;
using System.IO;
using System.Xml.Serialization;
using Photon.Common.Tools;
using Photon.LoadBalancing.MasterServer;

namespace Photon.LoadBalancing.Handler
{
    class SyncPlayerHandler : BaseHandler
    {

        public SyncPlayerHandler()
        {
            OpCode = ServerToServer.Operations.OperationCode.SyncPlayer;
        }
        public override void OnOperationRequest(OperationRequest operationRequest, SendParameters sendParameters, RedirectedClientPeer peer)
        {
            //取得在线客户,不包括当前用户
            List<string> usernameList = new List<string>();
            foreach (RedirectedClientPeer tempPeer in MasterApplication.Instance.peerList)
            {
                if (string.IsNullOrEmpty(tempPeer.username) == false && tempPeer != peer)
                {
                    usernameList.Add(tempPeer.username);
                }
            }

            //存储字符串
            StringWriter sw = new StringWriter();
            XmlSerializer serializer = new XmlSerializer(typeof(List<string>));
            serializer.Serialize(sw, usernameList);
            sw.Close();

            string usernameListString = sw.ToString();
            Dictionary<byte, object> data = new Dictionary<byte, object>();
            data.Add((byte)ParameterCode.UsernameKey, usernameListString);
            OperationResponse response = new OperationResponse(operationRequest.OperationCode);
            response.Parameters = data;
            peer.SendOperationResponse(response, sendParameters);


            foreach (RedirectedClientPeer tempPeer in MasterApplication.Instance.peerList)
            {
                if (string.IsNullOrEmpty(tempPeer.username) == false && tempPeer != peer)
                {
                    EventData ed = new EventData((byte)EventCode.NewPlayer);
                    Dictionary<byte, object> data2 = new Dictionary<byte, object>();
                    data2.Add((byte)ParameterCode.Username, peer.username);
                    ed.Parameters = data2;
                    tempPeer.SendEvent(ed, sendParameters);
                }
            }
        }
    }
}
