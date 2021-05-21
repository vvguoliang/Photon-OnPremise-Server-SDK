
namespace PhotonCloud.Authentication.AccountService
{
    using System;
    using System.Net;

    public class CustomWebClient : WebClient
    {
        private readonly int timeout;

        private readonly string username;

        private readonly string password; 
        
        public CustomWebClient(int timeout, string username, string password)
        {
            this.timeout = timeout;
            this.Proxy = null;
            this.username = username;
            this.password = password; 
        }

        public enum Result
        {
            Ok,
            Timeout,
            NotFound,
            Error
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address);
            if (request == null)
            {
                request = WebRequest.Create(address);
            }

            request.Proxy = null;
            request.Timeout = this.timeout;

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {

                string authInfo = string.Format("{0}:{1}", username, password);
                authInfo = Convert.ToBase64String(Encoding.GetBytes(authInfo));
                request.Headers["Authorization"] = "Basic " + authInfo;
            }

            return request;
        }

        
    }
}
