namespace CustomAuthService.Controllers
{
    using System.Collections.Generic;
    using System.Net.Http.Formatting;
    using System.Web.Http;

    public class ApiControllerBase : ApiController
    {
        protected FormDataCollection queryParams;

        protected void UpdateRequestParams()
        {
            if (!this.Request.Properties.ContainsKey("MS_QueryNameValuePairs"))
            {
                return;
            }

            var @params = this.Request.Properties["MS_QueryNameValuePairs"] as IEnumerable<KeyValuePair<string, string>>;

            queryParams = new FormDataCollection(@params);
        }
    }

}
