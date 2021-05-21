﻿using System.Collections.Generic;
using ExitGames.Concurrency.Fibers;
using Photon.Hive;
using Photon.Hive.Common;
using Photon.Hive.Plugin;
using Photon.Plugins.Common;

namespace Photon.LoadBalancing.GameServer
{
    public struct LBGameCreateOptions
    {
        private static readonly HttpRequestQueueOptions DefaultHttpRequestQueueOptions = new HttpRequestQueueOptions(
            GameServerSettings.Default.HttpQueueMaxErrors,
            GameServerSettings.Default.HttpQueueMaxTimeouts,
            GameServerSettings.Default.HttpQueueRequestTimeout,
            GameServerSettings.Default.HttpQueueQueueTimeout,
            GameServerSettings.Default.HttpQueueMaxBackoffTime,
            GameServerSettings.Default.HttpQueueReconnectInterval,
            GameServerSettings.Default.HttpQueueMaxQueuedRequests,
            GameServerSettings.Default.HttpQueueMaxConcurrentRequests);

        public GameCreateOptions GameCreateOptions { get; set; }
        public GameApplication Application { get; set; }

        public LBGameCreateOptions(GameApplication application,
            string gameId,
            Hive.Caching.RoomCacheBase roomCache = null,
            IPluginManager pluginManager = null,
            Dictionary<string, object> environment = null,
            ExtendedPoolFiber executionFiber = null,
            IPluginLogMessagesCounter logMessagesCounter = null
        )
            : this()
        {
            this.Application = application;
            this.GameCreateOptions = new GameCreateOptions(gameId, roomCache, pluginManager, GameServerSettings.Default.MaxEmptyRoomTTL)
            {
                HttpRequestQueueOptions = DefaultHttpRequestQueueOptions,
                Environment = environment,
                ExecutionFiber = executionFiber,
                LogMessagesCounter = logMessagesCounter
            };
        }
    }
}