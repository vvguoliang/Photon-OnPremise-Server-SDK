using System.Collections.Specialized;
using Photon.Common.Authentication.Data;

namespace PhotonCloud.Authentication.Data
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    public class ClientAuthenticationServiceInfo
    {
        [DataMember(IsRequired = true)]
        public ClientAuthenticationType AuthenticationType { get; set; }
        
        [DataMember(IsRequired = true)]
        public string AuthUrl { get; set; }

        [DataMember(IsRequired = false)]
        public bool RejectIfUnavailable { get; set; }

        [DataMember(IsRequired = false)]
        public string ParamsType { get; set; }

        private Dictionary<string, string> nameValuePairs;
        [DataMember(IsRequired = false)]
        public Dictionary<string,string> NameValuePairs
        {
            get
            {
                return this.nameValuePairs; 
            } 
            set
            {
                this.nameValuePairs = value;
                this.NameValuePairAsQueryString = GetNameValuePairsAsQueryString(value); 
            }
        }

        public string NameValuePairAsQueryString { get; private set; }

        public bool ForwardAsJSON { get { return this.ParamsType == FowardAsJSONValue; } }


        public ClientAuthenticationServiceInfo()
        {
            
        }

        public ClientAuthenticationServiceInfo(ClientAuthenticationType authenticationType, string requestUri, Dictionary<string, string> nameValuePairs)
        {
            this.AuthenticationType = authenticationType;
            this.AuthUrl = requestUri;
            
            this.NameValuePairs = nameValuePairs; 
        }

        private static string GetNameValuePairsAsQueryString(Dictionary<string, string> nameValuePairs)
        {
            if (nameValuePairs == null || nameValuePairs.Count == 0) return null;

            var httpValueCollection = System.Web.HttpUtility.ParseQueryString(string.Empty);
            foreach (var entry in nameValuePairs)
            {
                httpValueCollection.Add(entry.Key, entry.Value);
            }
            return httpValueCollection.ToString();
        }

        private const string ForwardAsJSONKey = "ParamsType";
        private const string FowardAsJSONValue = "PostJson";

        public void UpdateForwardAsJSONValue()
        {
            if (this.ForwardAsJSON)
            {
                return;
            }

            if (nameValuePairs == null || nameValuePairs.Count == 0)
            {
                return;
            }

            string value;
            if (this.nameValuePairs.TryGetValue(ForwardAsJSONKey, out value) && value == FowardAsJSONValue)
            {
                this.ParamsType = FowardAsJSONValue;
            }
            this.nameValuePairs.Remove(ForwardAsJSONKey);
            this.NameValuePairAsQueryString = GetNameValuePairsAsQueryString(this.nameValuePairs);
        }
    }
}
