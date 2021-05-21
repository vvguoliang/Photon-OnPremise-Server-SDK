namespace PhotonCloud.Authentication
{
    /// <summary>
    /// Set of authentication policies
    /// </summary>
    public static class TokenAuthLevel
    {
        private const byte Default = 0;
        /// <summary>
        /// auth with userId and password only on nameserver, master and game server will allow only auth requests with token.
        /// for all new applications
        /// </summary>
        public const byte AllowNonTokenAuthOnNameServer = Default;
        /// <summary>
        /// auth with userId and password allowed on master. game server will allow only auth requests with token. for old applications, 
        /// which do not support auth on nameserver and support token auth
        /// </summary>
        public const byte AllowNonTokenAuthOnMasterServer = 0x01;
        /// <summary>
        /// really old applications. auth with userId and password allowed on game server. token auth not supported at all
        /// </summary>
        public const byte AllowNonTokenAuthOnGameServer = 0x02;

        public static bool HasFlag(int value, byte flag)
        {
            return (value & flag) == flag;
        }
    }
}
