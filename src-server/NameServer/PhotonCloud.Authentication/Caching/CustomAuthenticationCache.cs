
using PhotonCloud.Authentication.CustomAuth;

namespace PhotonCloud.Authentication.Caching
{
    using System.Collections.Generic;

    using ExitGames.Logging;

    public class CustomAuthenticationCache
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, VAppsCustomAuthHandler> handlerDict = new Dictionary<string, VAppsCustomAuthHandler>();

        public VAppsCustomAuthHandler GetOrCreateHandler(string applicationId, IVACustomAuthCounters counters)
        {
            bool found = true;
            VAppsCustomAuthHandler handler;

            lock (this.handlerDict)
            {
                if (!this.handlerDict.TryGetValue(applicationId, out handler))
                {
                    found = false;
                    handler = new VAppsCustomAuthHandler(applicationId, null, null, counters);
                    this.handlerDict.Add(applicationId, handler);
                }
            }

            if (found == false && log.IsDebugEnabled)
            {
                log.DebugFormat("Created custom authentication handler for appId {0}", applicationId);
            }

            return handler;
        }
    }
}
