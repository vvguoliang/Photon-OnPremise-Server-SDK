namespace LoadTest
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;

    using ExitGames.Configuration;
    using ExitGames.Logging;

    using LoadTest.Diagnostics;
    using LoadTest.Properties;

    class ClientManager
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly List<Client> clients = new List<Client>();

        private Thread thread;

        private volatile bool stop;

        private readonly Application application;

        private static IPEndPoint nsEndPoint;
        private static string userNameTemplate;

        private readonly int begin;
        private readonly int end;

        private readonly int startupTime;
        private readonly int startInterval;


        private readonly LinkedList<Client> activeClients = new LinkedList<Client>();
        private readonly Queue<LinkedListNode<Client>> finishedClients = new Queue<LinkedListNode<Client>>();

        private static int indexBase;

        private readonly bool useSecondRun;

        private readonly int maxClientsCount;

        static ClientManager()
        {
            UpdateNSEndPoint();
            GenerateUserNameBase();
        }

        public ClientManager(Application application, int begin, int end, int startupTime)
        {
            this.application = application;
            this.begin = begin;
            this.end = end;
            this.startupTime = startupTime;

            var count = this.end - this.begin;
            if (count != 0)
            {
                this.startInterval = startupTime / count;
            }
        }

        public ClientManager(Application application, float startPerSecond, int maxClientsCount)
        {
            this.application = application;
            this.maxClientsCount = maxClientsCount;
            this.startInterval = (int)(1000 / startPerSecond);
            if (this.startInterval == 0)
            {
                this.startInterval = 1;
            }

            this.useSecondRun = true;
        }

        public void Run()
        {
            thread = new Thread(this.ThreadFun) { IsBackground = true };
            thread.Start(null);
            application.IncRunningManagers();
        }

        public void Stop()
        {
            stop = true;

            if (thread != null)
            {
                thread.Join(5000);
                thread = null;
            }
        }

        private void ThreadFun(object state)
        {
            if (this.useSecondRun)
            {
                this.run2();
            }
            else
            {
                this.run();
            }
        }

        private LinkedListNode<Client> GetNewClient()
        {
            if (this.finishedClients.Count == 0)
            {
                var client = new Client(this.application, nsEndPoint,
                    userNameTemplate + "_" + Interlocked.Increment(ref indexBase), this);
                var node = new LinkedListNode<Client>(client);
                client.Node = node;

                return node;
            }
            return this.finishedClients.Dequeue();
        }

        private void AddClient2()
        {
            if (this.activeClients.Count < this.maxClientsCount)
            {
                Counters.TotalClients.Increment();
                this.activeClients.AddLast(this.GetNewClient());
            }
        }

        public void TakeMeOut(Client client)
        {
            Counters.TotalClients.Decrement();
            this.activeClients.Remove(client.Node);
            if (this.finishedClients.Count < 100)
            {
                this.finishedClients.Enqueue(client.Node);
            }
            else
            {
                client.Close();
            }
        }

        private void run2()
        {
            if (stop)
            {
                return;
            }

            log.InfoFormat("We are starting to send requests to name server and will finish in {0} minutes",
                Settings.Default.TestDurationM);

            var durationS = Settings.Default.TestDurationM * 60 + this.startupTime / 1000;

            this.run2Body(10, this.startInterval * 30);
            this.run2Body(durationS, this.startInterval);

            log.Info("We finished send requests to name server");
            application.DecRunningManagers();
        }

        private void run2Body(int durationS, int startInter)
        {
            var timeWatch = Stopwatch.StartNew();
            var prev = timeWatch.ElapsedMilliseconds;
            var lastNewClient = prev - this.startInterval - 10;
            var stopCycle = false;

            while (!stopCycle)
            {
                var current = timeWatch.ElapsedMilliseconds;
                var delta = current - prev;
                prev = current;
                if (delta < 10)
                {
                    Thread.Sleep(10 - (int)delta);
                }

                while (current - lastNewClient > startInter)
                {
                    this.AddClient2();
                    lastNewClient += startInter;
                }

                var node = this.activeClients.First;
                while (node != null)
                {
                    var client = node.Value;
                    client.Do(delta);
                    node = node.Next;
                }

                stopCycle = (durationS < timeWatch.Elapsed.TotalSeconds) && !this.stop;
            }
        }

        private void run()
        {
            if (stop)
            {
                return;
            }

            log.InfoFormat("We are starting to send requests to name server and will finish in {0} minutes",
                Settings.Default.TestDurationM);

            var durationS = Settings.Default.TestDurationM * 60 + this.startupTime / 1000;

            this.runBody(durationS);

            log.Info("We finished send requests to name server");
            application.DecRunningManagers();
        }

        private void runBody(int durationS)
        {
            var timeWatch = Stopwatch.StartNew();
            var prev = timeWatch.ElapsedMilliseconds;
            long lastNewClient = prev - this.startInterval - 10;

            while (!this.stop)
            {
                var current = timeWatch.ElapsedMilliseconds;
                var delta = current - prev;
                prev = current;

                if (current - lastNewClient > this.startInterval)
                {
                    this.AddClient();
                    lastNewClient = current;
                }

                foreach (var client in this.clients)
                {
                    client.Do(delta);
                }
                Thread.Sleep(10);
                this.stop = (durationS < timeWatch.Elapsed.TotalSeconds);
            }
        }

        private void AddClient()
        {
            if (this.clients.Count != (this.end - this.begin))
            {
                Counters.TotalClients.Increment();
                var index = this.clients.Count;
                this.clients.Add(new Client(this.application, nsEndPoint, userNameTemplate + "_" + this.begin + index, this));
            }
        }

        private static void GenerateUserNameBase()
        {
            if ("%MACHINENAME%" == Settings.Default.UserBase)
            {
                userNameTemplate = Environment.MachineName;
            }
            else if ("%APPNAME" == Settings.Default.UserBase)
            {
                userNameTemplate = Application.Instance.ApplicationName;
            }
            else
            {
                userNameTemplate = Settings.Default.UserBase;
            }

            log.InfoFormat("User names templated generaged:{0}", userNameTemplate);
        }

        public static void UpdateNSEndPoint()
        {
            IPAddress masterAddress;
            if (!IPAddress.TryParse(Settings.Default.NameServerIPAddress, out masterAddress))
            {
                var hostEntry = Dns.GetHostEntry(Settings.Default.NameServerIPAddress);
                if (hostEntry.AddressList == null || hostEntry.AddressList.Length == 0)
                {
                    throw new ConfigurationException(
                        "MasterIPAddress setting is neither an IP nor an DNS entry: "
                        + Settings.Default.NameServerIPAddress);
                }

                masterAddress =
                    hostEntry.AddressList.First(address => address.AddressFamily == AddressFamily.InterNetwork);

                if (masterAddress == null)
                {
                    throw new ConfigurationException(
                        "MasterIPAddress does not resolve to an IPv4 address! Found: "
                        + string.Join(", ", hostEntry.AddressList.Select(a => a.ToString()).ToArray()));
                }
            }

            var masterPort = Settings.Default.NameServerPort;
            nsEndPoint = new IPEndPoint(masterAddress, masterPort);

            log.InfoFormat("NameServer end point updated:{0}", nsEndPoint);
        }

    }
}
