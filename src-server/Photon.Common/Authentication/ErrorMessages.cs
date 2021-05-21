namespace Photon.Common.Authentication
{
    public static class ErrorMessages
    {
        /// <summary>
        ///     If appId is set to null.
        /// </summary>
        public const string AppIdMissing = "Application id not set";

        public const string EmptyAppId = "Empty application id";

        public const string InternalError = "Internal server error";

        public const string InvalidAppIdFormat = "Invalid application id format";

        public const string InvalidAppId = "Invalid application id";

        public const string AuthTokenMissing = "Authentication token is missing";

        public const string AuthTokenInvalid = "Invalid authentication token";

        public const string AuthTokenEncryptionInvalid = "Invalid authentication token encryption";

        public const string AuthTokenExpired = "Authentication token expired";

        public const string AuthTokenTypeNotSupported = "Authentication token type not supported";

        public const string ProtocolNotSupported = "Network protocol not supported";

        public const string EmptyUserId = "UserId is null or empty";

        public const string InvalidTypeForAuthData = "Invalid type for auth data";

        public const string InvalidEncryptionData = "Encryption data are incomplete. ErrorMsg:{0}";

        public const string EncryptionModeMismatch = "Udp encryption is used for non Udp protocol";

        public const string InvalidAutenticationType = "Invalid authentication type. Only Token auth supported by master and gameserver";

        public const string ServerIsNotReady = "Server is not ready. Try to reconnect later";

        public const string InvalidEncryptionMode = "Requested encryption mode is not supported by server. RequestedMode={0}";

        public const string NonTokenAuthIsNotAllowed = "Non token authentication is not allowed";

        public const string TooManyVirtualAppVersions = "Too many application versions are in use";

        public const string RequestTimedout = "Request timed out";

        public const string AppIdNotFound = "AppId not found";

        public const string QueueTimeout = "Queue timeout";

        public const string QueueFull = "Queue full";

        public const string QueueOffline = "Queue offline";

        public const string CustomAuthUsedButNotSetup = "Custom Authenitcation is used but it is not set up for aplication";
    }
}