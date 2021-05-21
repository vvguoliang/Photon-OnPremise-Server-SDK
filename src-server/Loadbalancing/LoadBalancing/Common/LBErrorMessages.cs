using Photon.Hive.Operations;

namespace Photon.LoadBalancing.Common
{
// ReSharper disable once ClassNeverInstantiated.Global
    public class LBErrorMessages : HiveErrorMessages
    {
        public const string LobbiesLimitReached = "Limit of lobbies are reached. Can not create lobby";
        public const string LobbyTypesLenDoNotMatchLobbyNames = "LobbyTypes lenght does not match LobbyNames lenght";
        public const string LobbyTypesNotSet = "Lobby types not set";
        public const string FailedToGetServerInstance = "Failed to get server instance.";
        public const string LobbyNotJoined = "Lobby not joined";
        public const string NotAuthorized = "Not authorized";
        public const string Authenticating = "Already authenticating";
        public const string AlreadyAuthenticated = "Already authenticated";
        public const string RpcIsNotSetup = "Rpc Service isn't setup";
        public const string NotAllowedSemicolonInQuereyData = "Semicolon is not allowed in query data";
        public const string NotAllowedWordInQuereyData = "Word {0} is not allowed in query data";
        public const string SecureConnectionRequired = "According to server side setting user should use one of secure connection type - WSS, Encrypted UDP or others";
    }
}
