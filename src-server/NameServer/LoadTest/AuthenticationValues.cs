namespace LoadTest
{
    /// <summary>
    /// Options for optional "Custom Authentication" services used with Photon. Used by OpAuthenticate after connecting to Photon.
    /// </summary>
    public enum CustomAuthenticationType : byte
    {
        /// <summary>Use a custom authentification service. Currently the only implemented option.</summary>
        Custom = 0,

        /// <summary>Authenticates users by their Steam Account. Set auth values accordingly!</summary>
        Steam = 1,

        /// <summary>Authenticates users by their Facebook Account. Set auth values accordingly!</summary>
        Facebook = 2,

        /// <summary>Disables custom authentification. Same as not providing any AuthenticationValues for connect (more precisely for: OpAuthenticate).</summary>
        None = byte.MaxValue
    }

    /// <summary>
    /// Container for "Custom Authentication" values in Photon (default: user and token). Set AuthParameters before connecting - all else is handled.
    /// </summary>
    /// <remarks>
    /// Custom Authentication lets you verify end-users by some kind of login or token. It sends those
    /// values to Photon which will verify them before granting access or disconnecting the client.
    ///
    /// The Photon Cloud Dashboard will let you enable this feature and set important server values for it.
    /// https://cloud.exitgames.com/dashboard
    /// </remarks>
    public class AuthenticationValues
    {
        /// <summary>The type of custom authentication provider that should be used. Currently only "Custom" or "None" (turns this off).</summary>
        public CustomAuthenticationType AuthType = CustomAuthenticationType.Custom;

        /// <summary>This string must contain any (http get) parameters expected by the used authentication service. By default, username and token.</summary>
        /// <remarks>Standard http get parameters are used here and passed on to the service that's defined in the server (Photon Cloud Dashboard).</remarks>
        public string AuthParameters;

        /// <summary>Data to be passed-on to the auth service via POST. Default: null (not sent). Either string or byte[] (see setters).</summary>
        public object AuthPostData { get; private set; }

        /// <summary>Sets the data to be passed-on to the auth service via POST.</summary>
        /// <param name="byteData">Binary token / auth-data to pass on. Empty string will set AuthPostData to null.</param>
        public virtual void SetAuthPostData(string stringData)
        {
            this.AuthPostData = (string.IsNullOrEmpty(stringData)) ? null : stringData;
        }

        /// <summary>Sets the data to be passed-on to the auth service via POST.</summary>
        /// <param name="byteData">Binary token / auth-data to pass on.</param>
        public virtual void SetAuthPostData(byte[] byteData)
        {
            this.AuthPostData = byteData;
        }

        /// <summary>Creates the default parameter-string from a user- and token-value, escaping both. Alternatively set AuthParameters directly.</summary>
        /// <remarks>The default parameter string is: "username={user}&token={token}"</remarks>
        /// <param name="user">Name or other end-user ID used in custom authentication service.</param>
        /// <param name="token">Token provided by authentication service to be used on initial "login" to Photon.</param>
        public virtual void SetAuthParameters(string user, string token)
        {
            this.AuthParameters = "username=" + System.Uri.EscapeDataString(user) + "&token=" + System.Uri.EscapeDataString(token);
        }
    }
}
