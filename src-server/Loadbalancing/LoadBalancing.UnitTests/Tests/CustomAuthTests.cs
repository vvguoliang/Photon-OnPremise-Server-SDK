using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Web;
using ExitGames.Concurrency.Fibers;
using Newtonsoft.Json;
using NUnit.Framework;
using Photon.Common.Authentication;
using Photon.Common.Authentication.CustomAuthentication;
using Photon.Common.Authentication.Data;
using Photon.Common.Authentication.Diagnostic;
using Photon.LoadBalancing.Operations;
using Photon.Realtime;
using Photon.SocketServer;
using Photon.SocketServer.Net;
using ErrorCode = Photon.Common.ErrorCode;

#pragma warning disable 1570

namespace Photon.LoadBalancing.UnitTests.Tests
{

    [TestFixture]
    public class CustomAuthTests
    {
        private const string UserId = "UserId";
        private const string DashBoardParams = "p1=v1&p2=v2&p3=v3";
        private const string TestUrl = "http://test.url/";
        private const string ClientQueryString = "cp1=v1&cp2=v2";
        private const string ClientQueryStringWithDashboardIntersection = ClientQueryString + "&p1=v2";
        private const string PostDataString = "string";
        private readonly byte[] PostDataArray = new byte[] { 0, 1, 2, 3 };

        private const string PostDataTypeString = "string";
        private const string PostDataTypeArray = "array";
        private const string PostDataTypeDict = "dictionary";
        private const string PostDataTypeDictWithIntersect = "dictionary_intersect";
        private const string PostDataTypeOther = "other";
        private const string PostDataTypeNull = "Null";


        private readonly Dictionary<string, object> PostDataDictionary = new Dictionary<string, object>
        {
            {"dp1", "dv1"},
            {"dp2", "dv2"},
            {"dp3", "dv2"},
        };

        private readonly Dictionary<string, object> PostDataDictionaryWithDashBoardIngtersection = new Dictionary<string, object>
        {
            {"dp1", "dv1"},
            {"dp2", "dv2"},
            {"dp3", "dv2"},
            {"p1", "dv1"},
            {"p2", "dv2"},
            {"p3", "dv2"},
        };


        /// <summary>
        /// test covering cases 1) and 3) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        /// <param name="dashboardParams"></param>
        /// <param name="anonymous"></param>
        [Test]
        public void SuccessNoClientParamsNoPostData([Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = null
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ResutlPostData, Is.Null);
            Assert.That(queue.ResultContentType, Is.Null.Or.Empty);
            Assert.That(queue.ExecuteRequestCalled, Is.EqualTo(!anonymous));
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.EqualTo(anonymous));
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            if (anonymous)
            {
                Assert.That(queue.ResutlClientQueryStringParameters, Is.Null.Or.Empty);
                Assert.That(peer.ResultCustomAuthResult.ResultCode, Is.EqualTo(CustomAuthenticationResultCode.Ok));
            }
            else
            {
                var testString = dashboardParams != null ? TestUrl + "?" + DashBoardParams : TestUrl;
                Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(testString));
            }
        }

        /// <summary>
        /// test covering cases 2) and 4) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void SuccessClientParamsNoPostData([Values(ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ResutlPostData, Is.Null);
            Assert.That(queue.ResultContentType, Is.Null.Or.Empty);
            Assert.That(queue.ExecuteRequestCalled, Is.True);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            string testString;
            if (dashboardParams != null)
            {
                testString = TestUrl + "?" + dashboardParams + "&" + ClientQueryString;
            }
            else
            {
                testString = TestUrl + "?" + clientQueryString;
            }
            Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(testString));
        }

        /// <summary>
        /// test covering cases 5) and 7) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void SuccessNoClientParamsPostData([Values(PostDataTypeString, PostDataTypeArray)] object postData,
            [Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            postData = GetPostData((string)postData);

            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = null,
                ClientAuthenticationData = postData,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ResutlPostData, Is.Not.Null);
            Assert.That(queue.ResultContentType, Is.Null.Or.Empty);
            Assert.That(queue.ExecuteRequestCalled, Is.True);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            var testString = dashboardParams != null ? TestUrl + "?" + DashBoardParams : TestUrl;
            Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(testString));
        }

        /// <summary>
        /// test covering cases 6) and 8) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void SuccessClientParamsPostData([Values(ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(PostDataTypeString, PostDataTypeArray)] object postData,
            [Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            postData = GetPostData((string)postData);

            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = postData,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ResutlPostData, Is.Not.Null);
            Assert.That(queue.ResultContentType, Is.Null.Or.Empty);
            Assert.That(queue.ExecuteRequestCalled, Is.True);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            string testString;
            if (dashboardParams != null)
            {
                testString = TestUrl + "?" + dashboardParams + "&" + ClientQueryString;
            }
            else
            {
                testString = TestUrl + "?" + clientQueryString;
            }
            Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(testString));

        }


        /// <summary>
        /// test covering cases 9) and 11) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void SuccessNoClientParamsPostDataDictionary([Values(PostDataTypeDict, PostDataTypeDictWithIntersect)] string postData,
            [Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = null,
                ClientAuthenticationData = GetPostData(postData),
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ResutlPostData, Is.Not.Null);
            Assert.That(queue.ResultContentType, Is.EqualTo("application/json"));
            Assert.That(queue.ExecuteRequestCalled, Is.True);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            var testString = dashboardParams != null ? TestUrl + "?" + DashBoardParams : TestUrl;
            Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(testString));

            var json = Encoding.UTF8.GetString(queue.ResutlPostData);
            string jsonTestString;
            if (dashboardParams != null)
            {
                jsonTestString = Newtonsoft.Json.JsonConvert.SerializeObject(GetPostData(PostDataTypeDict));// duplicates will be removed
            }
            else
            {
                jsonTestString = Newtonsoft.Json.JsonConvert.SerializeObject(GetPostData(postData));
            }
            Assert.That(json, Is.EqualTo(jsonTestString));
        }


        /// <summary>
        /// test covering cases 10) and 12) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void SuccessClientParamsPostDataDictionary(
            [Values(ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(PostDataTypeDict, PostDataTypeDictWithIntersect)] string postData,
            [Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = GetPostData(postData),
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCalled, Is.True);
            Assert.That(queue.ResutlPostData, Is.Not.Null);
            Assert.That(queue.ResultContentType, Is.EqualTo("application/json"));
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            string testString;
            if (dashboardParams != null)// all duplicates will be removed from ClientQueryString
            {
                testString = TestUrl + "?" + dashboardParams + "&" + ClientQueryString;
            }
            else if (postData == PostDataTypeDictWithIntersect)// all duplicates will be removed from ClientQueryString
            {
                testString = TestUrl + "?" + ClientQueryString;
            }
            else
            {
                testString = TestUrl + "?" + clientQueryString;
            }
            Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(testString));

            var json = Encoding.UTF8.GetString(queue.ResutlPostData);
            string jsonTestString;
            if (dashboardParams != null)
            {
                jsonTestString = Newtonsoft.Json.JsonConvert.SerializeObject(GetPostData(PostDataTypeDict));// duplicates will be removed
            }
            else
            {
                jsonTestString = Newtonsoft.Json.JsonConvert.SerializeObject(GetPostData(postData));
            }
            Assert.That(json, Is.EqualTo(jsonTestString));

        }

        /// <summary>
        /// test covering cases 13) and 15) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        /// <param name="dashboardParams"></param>
        /// <param name="anonymous"></param>
        [Test]
        public void JSON_SuccessNoClientParamsNoPostData([Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, true);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = null
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCalled, Is.EqualTo(!anonymous));
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.EqualTo(anonymous));
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            if (anonymous)
            {
                Assert.That(queue.ResultContentType, Is.Null.Or.Empty);
                Assert.That(queue.ResutlPostData, Is.Null);
                Assert.That(queue.ResutlClientQueryStringParameters, Is.Null.Or.Empty);
                Assert.That(peer.ResultCustomAuthResult.ResultCode, Is.EqualTo(CustomAuthenticationResultCode.Ok));
            }
            else
            {
                Assert.That(queue.ResutlPostData, Is.Not.Null);
                Assert.That(queue.ResultContentType, Is.EqualTo("application/json"));

                Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(TestUrl));
                Assert.That(Encoding.UTF8.GetString(queue.ResutlPostData), Is.EqualTo(GetJSONFromQueryStrings(dashboardParams, null, null)));
            }
        }


        /// <summary>
        /// test covering cases 14) and 16) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void JSON_SuccessClientParamsNoPostData([Values(ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, true);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ResutlPostData, Is.Not.Null);
            Assert.That(queue.ResultContentType, Is.EqualTo("application/json"));
            Assert.That(queue.ExecuteRequestCalled, Is.True);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(TestUrl));

            string testString;
            if (dashboardParams != null)
            {
                testString = GetJSONFromQueryStrings(dashboardParams, ClientQueryString, GetPostData(PostDataTypeNull));
            }
            else
            {
                testString = GetJSONFromQueryStrings(null, clientQueryString, GetPostData(PostDataTypeNull));
            }

            Assert.That(Encoding.UTF8.GetString(queue.ResutlPostData), Is.EqualTo(testString));
        }

  
        /// <summary>
        /// test covering cases 21) and 24) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void JSON_SuccessClientParamsPostDataDictionary(
            [Values(null, ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(PostDataTypeDict, PostDataTypeDictWithIntersect)] string postData,// no null here. we have such test already
            [Values(null, DashBoardParams)] string dashboardParams, [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, true);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = GetPostData(postData),
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ResutlPostData, Is.Not.Null);
            Assert.That(queue.ResultContentType, Is.EqualTo("application/json"));
            Assert.That(queue.ExecuteRequestCalled, Is.True);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.False);

            Assert.That(queue.ResutlClientQueryStringParameters, Is.EqualTo(TestUrl));

            var testString = GetJSONFromQueryStrings(dashboardParams, clientQueryString, GetPostData(postData));

            Assert.That(Encoding.UTF8.GetString(queue.ResutlPostData), Is.EqualTo(testString));
        }

        /// <summary>
        /// test covering cases 17)-20), 25) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void JSON_Fail_PostDataOfUnsupportedType(
            [Values(null, ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(PostDataTypeString, PostDataTypeArray)] object postData,
            [Values(null, DashBoardParams)] string dashboardParams, 
            [Values(false, true)] bool anonymous)
        {
            postData = GetPostData((string)postData);

            var handler = new TestCustomAuthHandler(anonymous);
            var queue = handler.AddQueue(TestUrl, dashboardParams, true);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = postData,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ResutlPostData, Is.Null);
            Assert.That(queue.ResultContentType, Is.Null.Or.Empty);
            Assert.That(queue.ExecuteRequestCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
            Assert.That(handler.ErrorsCount, Is.EqualTo(1));
            Assert.That(queue.ResutlClientQueryStringParameters, Is.Null);
        }

        /// <summary>
        /// case 25) from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        /// <param name="clientQueryString"></param>
        /// <param name="dashboardParams"></param>
        /// <param name="anonymous"></param>
        /// <param name="validProvider"></param>
        [Test]
        public void FailAllCombinationsWithWrongPostDataType(
            [Values(ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(null, DashBoardParams)] string dashboardParams, 
            [Values(false, true)] bool anonymous,
            [Values(false, true)] bool validProvider
            )
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = GetPostData(PostDataTypeOther),
                ClientAuthenticationType = validProvider ? (byte)0 :  (byte)ClientAuthenticationType.Facebook,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCalled, Is.False);
            Assert.That(queue.ResutlPostData, Is.Null);
            Assert.That(queue.ResultContentType, Is.Null);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.False);
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
            Assert.That(peer.ResultErrorCode, Is.EqualTo(ErrorCode.CustomAuthenticationFailed));
        }

        /// <summary>
        /// we check that if account has multiple auth providers then they also work fine
        /// </summary>
        /// <param name="clientQueryString"></param>
        /// <param name="postData"></param>
        /// <param name="dashboardParams"></param>
        /// <param name="anonymous"></param>
        [Test]
        public void Success_MultiAuthTypesTest(
            [Values(null, ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(PostDataTypeString, PostDataTypeArray, PostDataTypeDict, PostDataTypeDictWithIntersect)] object postData,
            [Values(null, DashBoardParams)] string dashboardParams,
            [Values(false, true)] bool anonymous)
        {
            postData = GetPostData((string)postData);

            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams);
            var psQueue = handler.AddQueue(TestUrl, ClientQueryString, authType: ClientAuthenticationType.PlayStation);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = postData,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            Assert.That(psQueue.ExecuteRequestCallsCount, Is.EqualTo(0));

            authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = postData,
                ClientAuthenticationType = (byte)ClientAuthenticationType.PlayStation,
            };

            peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            Assert.That(psQueue.ExecuteRequestCallsCount, Is.EqualTo(1));
        }

        /// <summary>
        /// we check that if account has multiple auth providers with json enabled then they also work fine
        /// </summary>
        /// <param name="clientQueryString"></param>
        /// <param name="postData"></param>
        /// <param name="dashboardParams"></param>
        /// <param name="anonymous"></param>
        [Test]
        public void JSON_Success_MultiAuthTypesTest(
            [Values(null, ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(PostDataTypeDict, PostDataTypeDictWithIntersect)] object postData,
            [Values(null, DashBoardParams)] string dashboardParams,
            [Values(false, true)] bool anonymous)
        {
            postData = GetPostData((string)postData);

            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, forwardAsJSON: true);
            var psQueue = handler.AddQueue(TestUrl, ClientQueryString, forwardAsJSON: true, authType: ClientAuthenticationType.PlayStation);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = postData,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            Assert.That(psQueue.ExecuteRequestCallsCount, Is.EqualTo(0));

            authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = postData,
                ClientAuthenticationType = (byte)ClientAuthenticationType.PlayStation,
            };

            peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            Assert.That(psQueue.ExecuteRequestCallsCount, Is.EqualTo(1));
        }

        [Test]
        public void Fail_SuccessForCustomProvider_FailForFacebookTest(
            [Values(null, ClientQueryString, ClientQueryStringWithDashboardIntersection)] string clientQueryString,
            [Values(PostDataTypeString, PostDataTypeArray)] object postData,
            [Values(null, DashBoardParams)] string dashboardParams,
            [Values(false, true)] bool anonymous)
        {
            postData = GetPostData((string)postData);

            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, forwardAsJSON: false);
            var facebookQueue = handler.AddQueue(TestUrl, ClientQueryString, forwardAsJSON: true, authType: ClientAuthenticationType.Facebook);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = postData,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            Assert.That(facebookQueue.ExecuteRequestCallsCount, Is.EqualTo(0));

            authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = postData,
                ClientAuthenticationType = (byte)ClientAuthenticationType.Facebook,
            };

            peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            Assert.That(facebookQueue.ExecuteRequestCallsCount, Is.EqualTo(0));
            Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
            Assert.That(handler.ErrorsCount, Is.EqualTo(1));
        }

        /// <summary>
        /// tests are covering case 26
        /// if we do not send client params and client post data other settings does not matter
        /// from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        [Test]
        public void SuccessfulAnonym_Case_26(
            [Values(null, DashBoardParams)] string dashboardParams,
            [Values(false, true)] bool json)
        {
            var handler = new TestCustomAuthHandler(true);

            var queue = handler.AddQueue(TestUrl, dashboardParams, json);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationType = (byte)ClientAuthenticationType.Facebook,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(queue.ExecuteRequestCalled, Is.False);
            Assert.That(queue.ResutlPostData, Is.Null);
            Assert.That(queue.ResultContentType, Is.Null);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.True);
            Assert.That(peer.ResultErrorCode, Is.EqualTo(ErrorCode.Ok));
        }

        /// <summary>
        /// tests are covering case 26 if anonym == true. 30 - anonym == false 
        /// from https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// just one more test
        /// </summary>
        /// <param name="anonymous"></param>
        [Test]
        public void NonExistingAuthTypeNoQueryNoData([Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var facebookQueue = handler.AddQueue(TestUrl, DashBoardParams, true, ClientAuthenticationType.Facebook);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            Assert.That(facebookQueue.ExecuteRequestCallsCount, Is.EqualTo(0));
            if (anonymous)
            {
                Assert.That(peer.OnCustomAuthenticationResultCalled, Is.True);
                Assert.That(peer.ResultErrorCode, Is.EqualTo(ErrorCode.Ok));
            }
            else
            {
                Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
                Assert.That(peer.ResultErrorCode, Is.EqualTo(ErrorCode.CustomAuthenticationFailed));
            }
        }

        /// <summary>
        /// tests are covering 27-30 https://confluence.exitgames.com/display/PTN/Custom+Authentication+Behaviour+Cheatsheet
        /// </summary>
        /// <param name="authType"></param>
        /// <param name="clientQueryString"></param>
        /// <param name="postData"></param>
        [Test]
        public void WrongProvidersOrWronUrlTest(
            [Values(CustomAuthenticationType.Custom, CustomAuthenticationType.Facebook, CustomAuthenticationType.PlayStation)]
            CustomAuthenticationType authType,
            [Values(null, ClientQueryString)] string clientQueryString,
            [Values(PostDataTypeNull, PostDataString, PostDataTypeArray, PostDataTypeDict, PostDataTypeOther)] string postData,
            [Values(false, true)] bool wrongUrl,
            [Values(false, true)] bool json
            )
        {
            var handler = new TestCustomAuthHandler(true);
            if (wrongUrl)
            {
                handler.AddQueue("wrong.url", DashBoardParams, json, (ClientAuthenticationType) authType);
            }

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationType = (byte)authType,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = GetPostData(postData)
            };

            var peer = new TestCustomAuthPeer();
            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            if (postData == PostDataTypeNull && clientQueryString == null)
            {
                //anonymous was ignored, test fails (wrongUrl = true required because only then handler is added, which is checked in CustomAuthHandler.OnAuthenticateClient)
                if (authType == CustomAuthenticationType.PlayStation && wrongUrl)
                {
                    Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
                    Assert.That(peer.ResultErrorCode, Is.EqualTo(ErrorCode.CustomAuthenticationFailed));
                }
                else
                {
                    Assert.That(peer.OnCustomAuthenticationResultCalled, Is.True);
                    Assert.That(peer.ResultErrorCode, Is.EqualTo(ErrorCode.Ok));
                }
            }
            else
            {
                Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
                Assert.That(peer.ResultErrorCode, Is.EqualTo(ErrorCode.CustomAuthenticationFailed));
            }
        }

        [Test]
        public void NewtonsoftJson_6_to_11plus_regressionTest(
            [Values(ClientQueryString)] string clientQueryString,
            [Values(PostDataTypeNull)] string postData,
            [Values(false)] bool wrongUrl,
            [Values(true)] bool json
            )
        {
            var jsonResponse = "{\"Message\":\"Success\",\"ResultCode\":1.0,\"UserId\":\"5c891ecec1524a05168e9491\",\"Nickname\":\"flibble\"}";


            var handler = new TestCustomAuthHandler(true);
            handler.AddQueue("doesnt.matter", DashBoardParams, json, (ClientAuthenticationType)CustomAuthenticationType.Custom, jsonResponse);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationType = (byte)CustomAuthenticationType.Custom,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationData = GetPostData(postData)
            };

            var peer = new TestCustomAuthPeer();
            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);
            Assert.That(peer.OnCustomAuthenticationResultCalled, Is.True);
            Assert.That(peer.ResultErrorCode, Is.EqualTo(ErrorCode.Ok));
        }

        [Test]
        public void FacebookTests(
            [Values(null, "foo=bar", "token=7890")] string clientQueryString,
            [Values(null, "foo=bar", "appid=123&secret=456")] string dashboardParams,
            [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, authType: ClientAuthenticationType.Facebook);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationType = (byte)ClientAuthenticationType.Facebook,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            //anonymous
            if (anonymous && clientQueryString == null)
            {
                Assert.That(peer.OnCustomAuthenticationResultCalled, Is.True);
                Assert.That(handler.ErrorsCount, Is.EqualTo(0));
            }
            //required parameters supplied (values don't matter for test)
            else if (clientQueryString != null &&
                     dashboardParams != null &&
                     clientQueryString.Contains("token=") &&
                     dashboardParams.Contains("appid=") &&
                     dashboardParams.Contains("secret="))
            {
                Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            }
            else
            {
                Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(0));
                Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
                Assert.That(handler.ErrorsCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void SteamTests(
            [Values(null, "foo=bar", "ticket=7890")] string clientQueryString,
            [Values(null, "foo=bar", "appid=123&apiKeySecret=456")] string dashboardParams,
            [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, authType: ClientAuthenticationType.Steam);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationType = (byte)ClientAuthenticationType.Steam,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            //required parameters supplied (values don't matter for test)
            if (clientQueryString != null &&
                     dashboardParams != null &&
                     clientQueryString.Contains("ticket=") &&
                     dashboardParams.Contains("appid=") &&
                     dashboardParams.Contains("apiKeySecret="))
            {
                Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            }
            else
            {
                Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(0));
                Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
                Assert.That(handler.ErrorsCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void OculusTests(
            [Values(null, "foo=bar", "userid=7890&nonce=123456")] string clientQueryString,
            [Values(null, "foo=bar", "appid=123&appsecret=456")] string dashboardParams,
            [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, authType: ClientAuthenticationType.Oculus);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationType = (byte)ClientAuthenticationType.Oculus,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            //required parameters supplied (values don't matter for test)
            if (clientQueryString != null &&
                     dashboardParams != null &&
                     clientQueryString.Contains("userid=") &&
                     clientQueryString.Contains("nonce=") &&
                     dashboardParams.Contains("appid=") &&
                     dashboardParams.Contains("appsecret="))
            {
                Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            }
            else
            {
                Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(0));
                Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
                Assert.That(handler.ErrorsCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void ViveportTests(
            [Values(null, "foo=bar", "usertoken=7890")] string clientQueryString,
            [Values(null, "foo=bar", "appid=123&appsecret=456")] string dashboardParams,
            [Values(false, true)] bool anonymous)
        {
            var handler = new TestCustomAuthHandler(anonymous);

            var queue = handler.AddQueue(TestUrl, dashboardParams, authType: ClientAuthenticationType.Viveport);

            var authRequest = new AuthenticateRequest
            {
                UserId = UserId,
                ClientAuthenticationParams = clientQueryString,
                ClientAuthenticationType = (byte)ClientAuthenticationType.Viveport,
            };

            var peer = new TestCustomAuthPeer();

            handler.AuthenticateClient(peer, authRequest, new AuthSettings(), new SendParameters(), null);

            //required parameters supplied (values don't matter for test)
            if (clientQueryString != null &&
                     dashboardParams != null &&
                     clientQueryString.Contains("usertoken=") &&
                     dashboardParams.Contains("appid=") &&
                     dashboardParams.Contains("appsecret="))
            {
                Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(1));
            }
            else
            {
                Assert.That(queue.ExecuteRequestCallsCount, Is.EqualTo(0));
                Assert.That(peer.OnCustomAuthenticationErrorCalled, Is.True);
                Assert.That(handler.ErrorsCount, Is.EqualTo(1));
            }
        }

        #region Helpers

        private object GetPostData(string requested)
        {
            switch (requested)
            {
                case PostDataTypeArray:
                    return this.PostDataArray;
                case PostDataTypeString:
                    return PostDataString;
                case PostDataTypeDict:
                    return new Dictionary<string, object>(this.PostDataDictionary);
                case PostDataTypeDictWithIntersect:
                    return new Dictionary<string, object>(this.PostDataDictionaryWithDashBoardIngtersection);
                case PostDataTypeOther:
                    return 1;
                case PostDataTypeNull:
                    return null;
                default:
                    Assert.Fail("Unknown type of post data - {0}", requested);
                    break;
            }

            return null;
        }

        private static string GetJSONFromQueryStrings(string dashboardParams, string clientQueryString, object getPostData)
        {
            var dictionary = new Dictionary<string, object>();

            if (getPostData != null)
            {
                dictionary = new Dictionary<string, object>((IDictionary<string, object>)getPostData);
            }
            var collection = HttpUtility.ParseQueryString(string.Empty + clientQueryString);

            for (int i = 0; i < collection.Count; i++)
            {
                if (!dictionary.ContainsKey(collection.GetKey(i)))
                {
                    dictionary.Add(collection.GetKey(i), collection.Get(i));
                }
            }

            collection = HttpUtility.ParseQueryString(string.Empty + dashboardParams);

            for (int i = 0; i < collection.Count; i++)
            {
                dictionary[collection.GetKey(i)] = collection.Get(i);
            }
            return JsonConvert.SerializeObject(dictionary);
        }

        #endregion
    }

    class TestCustomAuthPeer : ICustomAuthPeer
    {
        public TestCustomAuthPeer()
        {
            this.ConnectionId = 0;
        }

        public int ConnectionId { get; private set; }
        public string UserId { get; set; }
        public bool OnCustomAuthenticationResultCalled { get; internal set; }
        public bool OnCustomAuthenticationErrorCalled { get; internal set; }
        public CustomAuthenticationResult ResultCustomAuthResult { get; set; }
        public ErrorCode ResultErrorCode { get; set; }


        public void OnCustomAuthenticationError(ErrorCode errorCode, string debugMessage, IAuthenticateRequest authenticateRequest, SendParameters sendParameters)
        {
            this.OnCustomAuthenticationErrorCalled = true;
            this.ResultErrorCode = errorCode;
        }

        public void OnCustomAuthenticationResult(CustomAuthenticationResult customAuthResult, IAuthenticateRequest authenticateRequest,
            SendParameters sendParameters, object state)
        {
            this.OnCustomAuthenticationResultCalled = true;
            this.ResultCustomAuthResult = customAuthResult;
        }
    }

    class TestClientAuthQueue : IClientAuthenticationQueue
    {
        private readonly string JsonTestResponse;

        public TestClientAuthQueue(string uri, string queryStringParameters, bool forwardAsJSON)
        {
            this.Uri = uri;
            this.QueryStringParameters = queryStringParameters;
            if (!string.IsNullOrEmpty(queryStringParameters))
            {
                this.QueryStringParametersCollection = HttpUtility.ParseQueryString(queryStringParameters);
            }
            this.ForwardAsJSON = forwardAsJSON;
            this.ClientAuthenticationType = ClientAuthenticationType.Custom;
        }

        public TestClientAuthQueue(string uri, string queryStringParameters, bool forwardAsJSON, string jsonTestResponse) : this (uri, queryStringParameters, forwardAsJSON)
        {
            this.JsonTestResponse = jsonTestResponse;
        }

        public NameValueCollection QueryStringParametersCollection { get; private set; }
        public string Uri { get; private set; }
        public string QueryStringParameters { get; private set; }
        public bool RejectIfUnavailable { get; private set; }
        public bool ForwardAsJSON { get; private set; }
        public ClientAuthenticationType ClientAuthenticationType { get; private set; }

        public string ResutlClientQueryStringParameters { get; private set; }
        public byte[] ResutlPostData { get; private set; }
        public string ResultContentType { get; private set; }
        public bool ExecuteRequestCalled { get; private set; }
        public int ExecuteRequestCallsCount { get; private set; }

        public object CustomData { get; set; }
        public void EnqueueRequest(string clientQueryStringParamters, byte[] postData, string contentType, Action<AsyncHttpResponse, IClientAuthenticationQueue> callback, object state)
        {
            this.ExecuteRequestCalled = true;
            ++this.ExecuteRequestCallsCount;
            this.ResutlClientQueryStringParameters = clientQueryStringParamters;
            this.ResutlPostData = postData;
            this.ResultContentType = contentType;
            this.RejectIfUnavailable = true;

            if (this.Uri.Contains("wrong"))
            {
                callback(new AsyncHttpResponse(HttpRequestQueueResultCode.Error, this.RejectIfUnavailable, state), this);
            }

            if(!string.IsNullOrEmpty(this.JsonTestResponse))
            {
                callback(new TestAsyncHttpResponse(HttpRequestQueueResultCode.Success, this.RejectIfUnavailable, state, Encoding.UTF8.GetBytes(this.JsonTestResponse)), this);
            }
        }

        public void EnqueueRequestWithExpectedStatusCodes(HttpWebRequest webRequest, byte[] postData, Action<AsyncHttpResponse, IClientAuthenticationQueue> callback, object state, List<HttpStatusCode> expectedStatusCodes)
        {
            throw new NotImplementedException();
        }
    }

    class TestCustomAuthHandler : CustomAuthHandler
    {
        class Fiber : IFiber
        {
            public void Enqueue(Action action)
            {
                action();
            }

            public void Start()
            {

            }

            public void RegisterSubscription(IDisposable toAdd)
            {

            }

            public bool DeregisterSubscription(IDisposable toRemove)
            {
                throw new NotImplementedException();
            }

            public IDisposable Schedule(Action action, long firstInMs)
            {
                throw new NotImplementedException();
            }

            public IDisposable ScheduleOnInterval(Action action, long firstInMs, long regularInMs)
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
            }
        }

        public int ErrorsCount;

        public TestCustomAuthHandler(bool anonymous = false) : base(null, new Fiber())
        {
            this.IsAnonymousAccessAllowed = anonymous;
        }

        public TestClientAuthQueue AddQueue(string url, string clientQueryString, bool forwardAsJSON = false, ClientAuthenticationType authType = ClientAuthenticationType.Custom, string jsonResponse = null)
        {
            TestClientAuthQueue queue;
            if (string.IsNullOrEmpty(jsonResponse))
            {
                queue = new TestClientAuthQueue(url, clientQueryString, forwardAsJSON);
            }
            else
            {
                queue = new TestClientAuthQueue(url, clientQueryString, forwardAsJSON, jsonResponse);

            }

            this.AddNewAuthProvider(authType, queue);

            return queue;
        }

        protected override void IncrementErrors(ClientAuthenticationType authenticationType, CustomAuthResultCounters instance)
        {
            ++this.ErrorsCount;
        }
    }

    public class TestAsyncHttpResponse : AsyncHttpResponse
    {
        public TestAsyncHttpResponse(HttpRequestQueueResultCode status, bool rejectIfUnavailable, object state, byte[] responseData) : base(status, rejectIfUnavailable, state)
        {
            this.ResponseData = responseData;
        }
    }

}
