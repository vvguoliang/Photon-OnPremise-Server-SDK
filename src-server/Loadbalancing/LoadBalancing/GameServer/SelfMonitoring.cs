namespace Photon.LoadBalancing.GameServer
{
    #region directives

    using System;
    using System.Collections.Generic;

    using ExitGames.Logging;

    using Photon.Common.Authentication;
    using Photon.LoadBalancing.Operations;

    #endregion

    //TODO sendInterval > pass to clients, call service() more frequent and let client check if enough time passed to raise next event
    public class SelfMonitoring
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private const string prefix = "SM";

        private AuthTokenFactory authTokenFactory;

        private string gameIP;
        private int gamePort;
        
        //settings
        private string appId;
        private int numGames;
        private int numClients;
        private int sendInterval;

        private List<TestClient> clients; 

        public SelfMonitoring(string settings, string gameIP, int gamePort, AuthTokenFactory authTokenFactory)
        {
            var split = settings.Split(';');

            if (split.Length != 4)
            {
                log.WarnFormat("SelfMonitoring, settings length expected to be 4, was {0}: {1}", split.Length, settings);
                return;
            }

            this.appId = split[0];
            if (!int.TryParse(split[1], out numGames))
            {
                log.WarnFormat("SelfMonitoring, cannot parse '{0}' (numGames)", split[1]);
                return;
            }
            if (!int.TryParse(split[2], out numClients))
            {
                log.WarnFormat("SelfMonitoring, cannot parse '{0}' (numClients)", split[2]);
                return;
            }
            if (!int.TryParse(split[3], out sendInterval))
            {
                log.WarnFormat("SelfMonitoring, cannot parse '{0}' (sendInterval)", split[3]);
                return;
            }

            this.gameIP = gameIP;
            this.gamePort = gamePort;

            this.authTokenFactory = authTokenFactory;

            clients = new List<TestClient>();

            if (log.IsInfoEnabled)
            {
                log.InfoFormat("SelfMonitoring, appId '{0}', numGames {1}, numClients {2}, sendInterval {3}, machineName {4}, gameIP {5}, gamePort {6}",
                    appId, numGames, numClients, sendInterval, Environment.MachineName, gameIP, gamePort);
            }
        }

        public void StartGames()
        {


            for (int i = 0; i < numGames; i++)
            {
                var gameName = string.Format("{0}_{1}_game_{2}", prefix, Environment.MachineName, i);

                for (int j = 0; j < numClients; j++)
                {
                    var userId = string.Format("{0}_{1}_user_{2}_{3}", prefix, Environment.MachineName, i, j);
                    var client = new TestClient();
                    client.Start(gameIP, gamePort, userId, gameName, GetToken(userId), sendInterval);

                    //TODO store clients grouped by game?
                    clients.Add(client);
                }
            }

            if (log.IsInfoEnabled)
            {
                log.InfoFormat("SelfMonitoring, started {0} games with {1} clients each", numGames, numClients);
            }
        }

        public void StopGames()
        {
            foreach (var testClient in clients)
            {
                testClient.Stop();
            }
            log.InfoFormat("SelfMonitoring, stopped all monitoring games");
        }

        public void Service()
        {
            var disconnectedClients = 0;

            foreach (var testClient in clients)
            {
                if (testClient.GetConnectionState() == TestClientConnectionState.Disconnected)
                {
                    disconnectedClients++;
                }

                testClient.Service();
            }

            if (disconnectedClients > 0)
            {
                if (log.IsInfoEnabled)
                {
                    log.InfoFormat("SelfMonitoring, {0} disonnected clients from {1} total", disconnectedClients, clients.Count);
                }
            }
        }

        private string GetToken(string userId)
        {
            var authRequest = new AuthenticateRequest
            {
                ApplicationId = this.appId,
                UserId = userId,
            };
            var authToken = authTokenFactory.CreateAuthenticationToken(userId, authRequest);
            var token = authTokenFactory.EncryptAuthenticationToken(authToken, false);
            return token;
        }
    }
}
