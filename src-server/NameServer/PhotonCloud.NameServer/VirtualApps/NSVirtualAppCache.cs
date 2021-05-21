using System.Collections.Generic;
using ExitGames.Logging;

namespace PhotonCloud.NameServer.VirtualApps
{
    public class NSVirtualAppCache
    {
        #region Constants and Fields

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        
        private readonly Dictionary<string, NSVirtualApp> dictionary = new Dictionary<string, NSVirtualApp>();

        #endregion

        #region .ctr

        public NSVirtualAppCache()
        {}

        #endregion

        #region Methods

        public NSVirtualApp GetOrCreateVirtualApp(string appId)
        {
            NSVirtualApp result;
            lock (this.dictionary)
            {
                if (!this.dictionary.TryGetValue(appId, out result))
                {
                    result = new NSVirtualApp(appId);
                    this.dictionary.Add(appId, result);

                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("Nameserver virtual app is created. appId:{0}", appId);
                    }
                }
            }

            return result;
        }

        #endregion
    }
}
