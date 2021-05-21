namespace CustomAuthService.Controllers
{
    using System;
    using System.Threading;
    using System.Web.Http;

    using ExitGames.Logging;

    public class AuthResponse
    {
        public int ResultCode { get; set; }
        public string Message { get; set; }
    }

    public class AuthController : ApiControllerBase
    {
        private static volatile int requestNumber;
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly Random rnd = new Random();
        // GET api/values 
        public IHttpActionResult Get()
        {
            ++requestNumber;

            var config = ((Application)Application.Instance).GetConfig();
            if (config.FixedError != 0 && requestNumber%config.FixedError == 0)
            {
                log.Info("request rejected");
                return this.NotFound();
            }

            this.UpdateRequestParams();
            var value = queryParams["username"];
            AuthResponse response;
            if (value == "yes")
            {
                response = new AuthResponse
                {
                    ResultCode = 1,
                    Message = "Ok"
                };
            }
            else if (value == "method1")
            {
                response = new AuthResponse
                {
                    ResultCode = 0,
                    Message = ""
                };
            }
            else
            {
                response = new AuthResponse
                {
                    ResultCode = 2,
                    Message = "Fail: user not found"
                };
            }

            var rndValue = this.rnd.Next(10000);
            if (rndValue <= config.Timeouts)
            {
                Thread.Sleep(config.SleepTime);
                return this.Json(response);
            }

            return this.Json(response);
        }

        //// POST api/values 
        //public void Post([FromBody]string value)
        //{
        //    this.Ok("Success");
        //}
    }
}
