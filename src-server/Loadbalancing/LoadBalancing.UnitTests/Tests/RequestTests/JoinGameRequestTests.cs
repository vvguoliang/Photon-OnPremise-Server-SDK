using NUnit.Framework;
using Photon.Hive.Operations;
using System.Collections;

namespace Photon.LoadBalancing.UnitTests.Tests.RequestTests
{
    [TestFixture]
    public class JoinGameRequestTests
    {
        [Test]
        public void JoinGameRequestTest()
        {
            var request = new Photon.SocketServer.OperationRequest((byte) OperationCode.JoinGame)
            {
                Parameters = new System.Collections.Generic.Dictionary<byte, object>()
                {
                    {(byte)ParameterKey.GameProperties, new Hashtable {{(byte)GameParameter.EmptyRoomTTL, null}}},
                    {(byte)ParameterKey.GameId, null},
                }
            };

            var requestObj = new CreateGameRequest(Photon.SocketServer.Protocol.GpBinaryV162, request, "");

        }
    }
}
