namespace CustomAuthService.Controllers
{
    using System.IO;
    using System.Web.Http;
    using System.Web.Http.Results;
    using System.Web.Script.Serialization;

    using Newtonsoft.Json;

    using Photon.SocketServer;

    using PhotonCloud.Authentication.Data;

    public class ConfigController : ApiControllerBase
    {
        // GET api/values 
        public JsonResult<TmpApplicationAccount> Get(string appId)
        {
            this.UpdateRequestParams();

            var path = Path.Combine(ApplicationBase.Instance.BinaryPath, "application.json");
            using (var reader = File.OpenText(path))
            {
                var result = reader.ReadToEnd();

                var applicationInfo = JsonConvert.DeserializeObject<TmpApplicationAccount>(result);

                return this.Json(applicationInfo);
            }
        }

        // POST api/values 
        public void Post([FromBody]string value)
        {
            this.Ok("Success");
        }
    }
}
