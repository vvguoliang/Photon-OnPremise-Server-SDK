// --------------------------------------------------------internal ------------------------------------------------------------
// <copyright file="CloudCache.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the CloudCache type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Linq;

namespace PhotonCloud.NameServer
{
    using System;
    using System.Collections.Generic;

    using ExitGames.Logging;

    using Photon.Common.Authentication;
    using Photon.SocketServer;

    using PhotonCloud.Authentication;
    using PhotonCloud.NameServer.Configuration;
    using Photon.Common.Authentication.Data;
    using PhotonCloud.NameServer.Operations;
    using Photon.NameServer.Operations;

    public class CloudMasterServerCache
    {
        private static readonly Random rnd = new Random();

        private static readonly char[] RegionSeparators = { '/' };

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly List<CloudPhotonEndpointInfo> servers = new List<CloudPhotonEndpointInfo>();
        private readonly Dictionary<string, bool> cluster0Presence = new Dictionary<string, bool>();

        public const string Cluster0Name = "cluster0";
        public const string DefaultClusterName = "default";
        public const string RandomClusterName = "*";

        public CloudMasterServerCache(IEnumerable<Node> nodes)
        {
            foreach (var nodeInfo in nodes)
            {
                var info = new CloudPhotonEndpointInfo(nodeInfo);
                this.servers.Add(info);

                var regionCloud = $"{info.Region}_{info.PrivateCloud}";
                foreach (var serviceType in nodeInfo.ServiceTypes)
                {
                    var key = $"{regionCloud}_{serviceType.ToString()}";
                    if (!this.cluster0Presence.TryGetValue(key, out bool presence) || !presence)
                    {
                        this.cluster0Presence[key] = info.Cluster == Cluster0Name;
                    }
                }
            }

            if (log.IsDebugEnabled)
            {
                foreach (var endpoint in this.servers)
                {
                    log.DebugFormat("Hostname - 2 {0}, UDP: {1}, HTTP: {2}", endpoint.UdpHostname, endpoint.UdpEndPoint, endpoint.HttpEndPoint);
                }
            }
        }

        public bool TryGetPhotonEndpoint(IAuthenticateRequest request, ApplicationAccount appAccount, out CloudPhotonEndpointInfo result, out string message)
        {
            result = null;
            message = null;

            if (string.IsNullOrEmpty(appAccount.PrivateCloud))
            {
                message = string.Format("No private cloud set for applicaton ID {0} - can not get Master", request.ApplicationId);
                log.Error(message);
                return false;
            }

            if (string.IsNullOrEmpty(request.Region))
            {
                message = string.Format("No region set in authenticate request for application ID {0} - can not determine Master", request.ApplicationId);
                log.Error(message);
                return false;
            }

            var privateCloud = appAccount.PrivateCloud;
            var requestedRegion = request.Region.ToLower();

            var cluster = DefaultClusterName;

            // if a cluster "*" has been passed then we return a random cluster
            // if an actual value has been passed we try to return that particular cluster
            if (requestedRegion.Contains("/"))
            {
                var regionArray = requestedRegion.Split(RegionSeparators, StringSplitOptions.RemoveEmptyEntries);
                if (regionArray.Length > 0)
                {
                    requestedRegion = regionArray[0];
                }

                // if account is enterprise account, we allow to select cluster using auth request
                if (regionArray.Length > 1 && appAccount.IsEnterprise)
                {
                    cluster = regionArray[1];
                }
            }

            var defaultAppCluster = DefaultClusterName;
            // app is cluster0 app and there is cluster0 in requested region then we set cluster0
            if (appAccount.IsAppForCluster0
                && this.cluster0Presence.TryGetValue(MakeCluster0IndexKey(appAccount, requestedRegion), out bool present)
                && present)
            {
                defaultAppCluster = Cluster0Name;
                cluster = Cluster0Name;
            }

            if (appAccount.RegionClusterInfos != null && appAccount.RegionClusterInfos.ContainsKey(requestedRegion))
            {
                var clusters = appAccount.RegionClusterInfos[requestedRegion];

                // if "*" has been passed we just chose a random one:
                if (RandomClusterName.Equals(cluster))
                {
                    cluster = clusters[rnd.Next(0, clusters.Count)];
                }
                else
                {
                    // check if a valid cluster has been found:
                    if (!clusters.Contains(cluster))
                    {
                        if (!clusters.Contains(defaultAppCluster))
                        {
                            cluster = clusters[0];
                        }
                        else
                        {
                            cluster = defaultAppCluster;
                        }
                    }
                }
            }

            //enterprise customer who send "*" and cluster was not replaced with value from RegionClusterInfos (because info is not set)
            if (RandomClusterName.Equals(cluster) && appAccount.IsEnterprise)
            {
                cluster = DefaultClusterName;
            }

            result = this.TryGetPhotonEndpoint(privateCloud, requestedRegion, cluster, appAccount.ServiceType);

            if (log.IsDebugEnabled)
            {
                if (result == null)
                {
                    log.Debug("No endpoint found");
                }
                else
                {
                    log.DebugFormat("Endpoint found 2 - Hostname {0}, UDP: {1}, HTTP: {2}", result.UdpHostname, result.UdpEndPoint, result.HttpEndPoint);
                }

            }

            return result != null;
        }

        public bool GetRegionList(GetRegionListRequest request, ApplicationAccount appAccount, NetworkProtocolType networkProtocol, int port, bool isIPv6, bool useHostnames,
            out List<string> regions, out List<string> endPoints, out string message)
        {
            regions = new List<string>();
            endPoints = new List<string>();
            message = null;

            // check submitted ID: 
            Guid appId;
            if (!Guid.TryParse(request.ApplicationId, out appId))
            {
                message = string.Format("Invalid Application ID format: {0}", request.ApplicationId);
                if (log.IsDebugEnabled)
                {
                    log.Debug(message);
                    return false;
                }
            }

            if (string.IsNullOrEmpty(appAccount.PrivateCloud))
            {
                message = string.Format("No private cloud set for applicaton ID {0} - can not get Master", request.ApplicationId);
                log.Error(message);
                return false;
            }

            var privateCloud = appAccount.PrivateCloud;
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("PrivateCloud: {0}", privateCloud);
                var infos = appAccount.RegionClusterInfos;
                if (infos == null)
                {
                    log.DebugFormat("RegionClusterInfos == null!");
                }
                else
                {
                    log.DebugFormat("RegionClusterInfos:");
                    foreach (var info in infos)
                    {
                        log.DebugFormat(info.Key);
                        foreach (var clusterInfo in info.Value)
                        {
                            log.DebugFormat("\t{0}", clusterInfo);
                        }
                    }
                }

            }

            var allPhotonEndpoints = this.GetAllPhotonEndpoints(privateCloud, appAccount.ServiceType, appAccount);

            //tmp for whitelist
            var allRegions = new List<string>();
            var allEndPoints = new List<string>();

            foreach (var server in allPhotonEndpoints)
            {
                var endpoint = server.GetEndPoint(networkProtocol, port, isIPv6, useHostnames);
                if (endpoint == null)
                {
                    continue;
                }

                string regionCluster = FormatRegionCultureString(server, allPhotonEndpoints);

                //use regionwhite list
                if (!string.IsNullOrEmpty(appAccount.GetRegionsFilter))
                {
                    //store all in case whitelist leaves no result
                    allRegions.Add(regionCluster);
                    allEndPoints.Add(endpoint);

                    if (!IsRegionClusterWhitelisted(appAccount.GetRegionsFilter, server.Region, server.Cluster))
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.DebugFormat("Whitelist does not contain regionCluster '{0}', skipping", regionCluster);
                        }
                        continue;
                    }
                }

                regions.Add(regionCluster);
                endPoints.Add(endpoint);

                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("RegionCluster: {0} -> Endpoint: {1}", regionCluster, endpoint);
                }
            }

            if (!string.IsNullOrEmpty(appAccount.GetRegionsFilter) && regions.Count == 0)
            {
                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Whitelist left no entries, ignoring whitelist, returning all {0} entries", allRegions.Count);
                }
                regions.AddRange(allRegions);
                endPoints.AddRange(allEndPoints);
            }

            if (isIPv6 && regions.Count == 0)
            {
                message = string.Format("No IPv6 capable Master found for applicaton ID {0}", request.ApplicationId);
                log.Error(message);
                return false;
            }

            return true;
        }

        //regionWhiteList: regionCluster1;regionCluster2;regionCluster3
        //allowed regionCluster formats: region/cluster or region (allows all cluster for region)
        private bool IsRegionClusterWhitelisted(string regionWhiteList, string region, string cluster)
        {
            //check for wildcard region. semicolon required to distinguish US and USW
            if (regionWhiteList.Contains(string.Format("{0};", region)))
            {
                return true;
            }

            //check for specific entry
            if (regionWhiteList.Contains(string.Format("{0}/{1};", region, cluster)))
            {
                return true;
            }

            return false;
        }

        private List<CloudPhotonEndpointInfo> GetAllPhotonEndpoints(string privateCloud, ServiceType serviceType,
            ApplicationAccount applicationAccount)
        {
            var serversByPrivateCloudAndServiceType =
                this.servers.FindAll(
                    serverConfig =>
                        serverConfig.PrivateCloud == privateCloud
                        && serverConfig.ServiceType.Contains(serviceType));

            var serversWithDuplicates = new List<CloudPhotonEndpointInfo>();

            var regionClusterInfo = applicationAccount.RegionClusterInfos;

            foreach (var server in serversByPrivateCloudAndServiceType)
            {
                if (regionClusterInfo != null)
                {
                    // we have a region/cluster filter specified: add only the "matching" cluster nodes. 
                    List<string> clusters;
                    if (regionClusterInfo.TryGetValue(server.Region, out clusters))
                    {
                        if (clusters.Contains(server.Cluster))
                        {
                            serversWithDuplicates.Add(server);
                        }
                        continue;
                    }
                }

                // if this is cluster0 app and we have cluster0 in this region
                var defaultCluster = applicationAccount.IsAppForCluster0
                    && this.cluster0Presence[MakeCluster0IndexKey(applicationAccount, server.Region)]
                    ? Cluster0Name : DefaultClusterName;

                // we have no region/cluster filter specified: add only "default" or "cluster0" nodes. 
                if (server.Cluster != defaultCluster)
                {
                    continue;
                }

                serversWithDuplicates.Add(server);
            }

            var result = new List<CloudPhotonEndpointInfo>();
            var comparer = new PhotonEndpointInfoComparer();

            // the result contains now ALL photonEndpoints, filtered by serviceType, Cloud. 
            //However, there might still be multiple entries for the same serviceType / Cloud / Cluster - we need to choose one by random. 
            foreach (var server in serversWithDuplicates)
            {
                var duplicates = serversWithDuplicates.FindAll(duplicate => comparer.Equals(duplicate, server));

                if (duplicates.Count > 0 && !result.Exists(x => comparer.Equals(x, server)))
                {
                    if (duplicates.Count == 1)
                    {
                        result.Add(duplicates[0]);
                    }
                    else
                    {
                        var firstD = duplicates[0];
                        log.ErrorFormat("There are {3} duplicated nodes in region '{0}', cloud: '{1}', cluster:'{2}'",
                            firstD.Region, firstD.PrivateCloud, firstD.Cluster, duplicates.Count);

                        // choose one of the duplicate servers by random. 
                        result.Add(this.GetServerFromDuplicates(duplicates));
                    }
                }
            }

            return result;
        }

        private static string MakeCluster0IndexKey(ApplicationAccount appAccount, string region)
        {
            return $"{region}_{appAccount.PrivateCloud}_{appAccount.ServiceType}";
        }

        private CloudPhotonEndpointInfo GetServerFromDuplicates(List<CloudPhotonEndpointInfo> duplicates)
        {
            var result = duplicates[0];
            var useHostNames = result.TcpEndPoint != null;

            if (useHostNames)
            {
                for (int i = 1; i < duplicates.Count; ++i)
                {
                    var d = duplicates[i];
                    if (result.TcpHostname.CompareTo(d.TcpHostname) > 0)
                    {
                        result = d;
                    }
                }
            }
            else
            {
                for (int i = 1; i < duplicates.Count; ++i)
                {
                    var d = duplicates[i];
                    if (result.TcpEndPoint.CompareTo(d.TcpEndPoint) > 0)
                    {
                        result = d;
                    }
                }
            }
            return result;
        }

        private CloudPhotonEndpointInfo TryGetPhotonEndpoint(string privateCloud, string region, string cluster, ServiceType serviceType)
        {
            // if serverConfig.ServiceTypes is not set, all service types are allowed.
            var serversWithDuplicates =
                this.servers.FindAll(
                    serverConfig =>
                    serverConfig.PrivateCloud == privateCloud &&
                    serverConfig.Region == region &&
                    serverConfig.Cluster == cluster &&
                    serverConfig.ServiceType.Contains(serviceType));

            if (serversWithDuplicates.Count == 1)
            {
                return serversWithDuplicates[0];
            }
            else if (serversWithDuplicates.Count > 1)
            {
                var firstD = serversWithDuplicates[0];
                log.ErrorFormat("There are {3} duplicated nodes in region '{0}', cloud: '{1}', cluster:'{2}'",
                    firstD.Region, firstD.PrivateCloud, firstD.Cluster, serversWithDuplicates.Count);

                // support multiple masters per server type / cloud / region / cluster. Choose one by random. 
                return this.GetServerFromDuplicates(serversWithDuplicates);
            }

            return null;
        }

        //for monitoring
        public List<CloudPhotonEndpointInfo> TryGetPhotonEndpoints(string privateCloud, ServiceType serviceType)
        {
            // if serverConfig.ServiceTypes is not set, all service types are allowed.
            var serversWithDuplicates =
                this.servers.FindAll(
                    serverConfig =>
                    serverConfig.PrivateCloud == privateCloud &&
                    serverConfig.ServiceType.Contains(serviceType));

            if (serversWithDuplicates.Count > 0)
            {
                return serversWithDuplicates;
            }

            return null;
        }

        private static string FormatRegionCultureString(CloudPhotonEndpointInfo server, List<CloudPhotonEndpointInfo> endpointInfos)
        {
            string regionCluster = server.Region;
            if (!string.IsNullOrEmpty(server.Cluster))
            {
                // check how many entries we have for that region, if it's more than one we need to add cluster:
                var count = endpointInfos.Count(e => regionCluster.Equals(e.Region, StringComparison.OrdinalIgnoreCase));
                if (count > 1)
                {
                    regionCluster = string.Format("{0}/{1}", server.Region, server.Cluster);
                }
            }

            return regionCluster;
        }
    }
}
