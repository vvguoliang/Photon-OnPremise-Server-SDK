namespace CustomAuthService
{
    using System.Web.Http;

    using Owin;

    public class Startup
    {
        // This code configures Web API. The Startup class is specified as a type
        // parameter in the WebApp.Start method.
        public void Configuration(IAppBuilder appBuilder)
        {
            // Configure Web API for self-host. 
            var config = new HttpConfiguration();
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{appId}",
                defaults: new { appId = RouteParameter.Optional }
            );

            appBuilder.UseWebApi(config);
        }
    }

    }
