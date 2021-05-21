using System;
using ExitGames.Diagnostics.Counter;
using ExitGames.Diagnostics.Monitoring;
using Photon.Common.Authentication.Data;

// ReSharper disable MemberCanBePrivate.Global

namespace PhotonCloud.Authentication.CustomAuth.Diagnostic
{
    /// <summary>
    /// Adds global photon counters speciefic for NameServer
    /// </summary>
    public static class PhotonCustomAuthCounters
    {
        #region Counters

        /// <summary>
        /// Number of 'Data' responses from custom auth service
        /// </summary>
        [PublishCounter("CustomAuthResultsData")]
        public static readonly NumericCounter ResultsData = new NumericCounter("CustomAuthResultsData");

        /// <summary>
        /// Number of 'OK' responses from custom auth service
        /// </summary>
        [PublishCounter("CustomAuthResultsAccepted")]
        public static readonly NumericCounter ResultsAccepted = new NumericCounter("CustomAuthResultsAccepted");

        /// <summary>
        /// Number of 'Failure' responses from custom auth service
        /// </summary>
        [PublishCounter("CustomAuthResultsDenied")]
        public static readonly NumericCounter ResultsDenied = new NumericCounter("CustomAuthResultsDenied");

        [PublishCounter("CustomAuthQueueTimeouts")]
        public static readonly AverageCounter QueueTimeouts = new AverageCounter("CustomAuthQueueTimeouts");

        [PublishCounter("CustomAuthQueueFullErrors")]
        public static readonly AverageCounter QueueFullErrors = new AverageCounter("CustomAuthQueueFullErrors");

        [PublishCounter("CustomAuthHttpRequestTime")]
        public static readonly AverageCounter HttpRequetsTime = new AverageCounter("CustomAuthHttpRequestTime");

        [PublishCounter("CustomAuthHttpRequests")] 
        public static readonly AverageCounter HttpRequests = new AverageCounter("CustomAuthHttpRequests");

        // totals
        [PublishCounter("CustomAuthErrors")]
        public static readonly AverageCounter Errors = new AverageCounter("CustomAuthErrors");

        [PublishCounter("CustomAuthHttpErrors")] 
        public static readonly AverageCounter HttpErrors = new AverageCounter("CustomAuthHttpErrors");
        
        [PublishCounter("CustomAuthHttpTimeouts")] 
        public static readonly AverageCounter HttpTimeouts = new AverageCounter("CustomAuthHttpTimeouts");
        
        // Custom
        [PublishCounter("CustomAuthErrors_Custom")]
        public static readonly AverageCounter ErrorsCustom = new AverageCounter("CustomAuthErrors_Custom");

        [PublishCounter("CustomAuthHttpErrors_Custom")] 
        public static readonly AverageCounter HttpErrorsCustom = new AverageCounter("CustomAuthHttpErrors_Custom");
        
        [PublishCounter("CustomAuthHttpTimeouts_Custom")] 
        public static readonly AverageCounter HttpTimeoutsCustom = new AverageCounter("CustomAuthHttpTimeouts_Custom");
        
        // Steam
        [PublishCounter("CustomAuthErrors_Steam")]
        public static readonly AverageCounter ErrorsSteam = new AverageCounter("CustomAuthErrors_Steam");

        [PublishCounter("CustomAuthHttpErrors_Steam")] 
        public static readonly AverageCounter HttpErrorsSteam = new AverageCounter("CustomAuthHttpErrors_Steam");
        
        [PublishCounter("CustomAuthHttpTimeouts_Steam")] 
        public static readonly AverageCounter HttpTimeoutsSteam = new AverageCounter("CustomAuthHttpTimeouts_Steam");
        
        // Facebook
        [PublishCounter("CustomAuthErrors_Facebook")]
        public static readonly AverageCounter ErrorsFacebook = new AverageCounter("CustomAuthErrors_Facebook");

        [PublishCounter("CustomAuthHttpErrors_Facebook")] 
        public static readonly AverageCounter HttpErrorsFacebook = new AverageCounter("CustomAuthHttpErrors_Facebook");
        
        [PublishCounter("CustomAuthHttpTimeouts_Facebook")] 
        public static readonly AverageCounter HttpTimeoutsFacebook = new AverageCounter("CustomAuthHttpTimeouts_Facebook");
        
        // Oculus
        [PublishCounter("CustomAuthErrors_Oculus")]
        public static readonly AverageCounter ErrorsOculus = new AverageCounter("CustomAuthErrors_Oculus");

        [PublishCounter("CustomAuthHttpErrors_Oculus")] 
        public static readonly AverageCounter HttpErrorsOculus = new AverageCounter("CustomAuthHttpErrors_Oculus");
        
        [PublishCounter("CustomAuthHttpTimeouts_Oculus")] 
        public static readonly AverageCounter HttpTimeoutsOculus = new AverageCounter("CustomAuthHttpTimeouts_Oculus");
        
        // PlayStation
        [PublishCounter("CustomAuthErrors_PlayStation")]
        public static readonly AverageCounter ErrorsPlayStation = new AverageCounter("CustomAuthErrors_PlayStation");

        [PublishCounter("CustomAuthHttpErrors_PlayStation")] 
        public static readonly AverageCounter HttpErrorsPlayStation = new AverageCounter("CustomAuthHttpErrors_PlayStation");
        
        [PublishCounter("CustomAuthHttpTimeouts_PlayStation")] 
        public static readonly AverageCounter HttpTimeoutsPlayStation = new AverageCounter("CustomAuthHttpTimeouts_PlayStation");
        
        // XBox
        [PublishCounter("CustomAuthErrors_XBox")]
        public static readonly AverageCounter ErrorsXBox = new AverageCounter("CustomAuthErrors_XBox");

        [PublishCounter("CustomAuthHttpErrors_XBox")] 
        public static readonly AverageCounter HttpErrorsXBox = new AverageCounter("CustomAuthHttpErrors_XBox");
        
        [PublishCounter("CustomAuthHttpTimeouts_XBox")] 
        public static readonly AverageCounter HttpTimeoutsXBox = new AverageCounter("CustomAuthHttpTimeouts_XBox");

        // PlayerIo
        [PublishCounter("CustomAuthErrors_PlayerIo")]
        public static readonly AverageCounter ErrorsPlayerIo = new AverageCounter("CustomAuthErrors_PlayerIo");

        // JWT
        [PublishCounter("CustomAuthErrors_Jwt")]
        public static readonly AverageCounter ErrorsJwt = new AverageCounter("CustomAuthErrors_Jwt");

        // Viveport
        [PublishCounter("CustomAuthErrors_Viveport")]
        public static readonly AverageCounter ErrorsViveport = new AverageCounter("CustomAuthErrors_Viveport");

        [PublishCounter("CustomAuthHttpErrors_Viveport")]
        public static readonly AverageCounter HttpErrorsViveport = new AverageCounter("CustomAuthHttpErrors_Viveport");

        [PublishCounter("CustomAuthHttpTimeouts_Viveport")]
        public static readonly AverageCounter HttpTimeoutsViveport = new AverageCounter("CustomAuthHttpTimeouts_Viveport");

        // Nintendo
        [PublishCounter("CustomAuthErrors_Nintendo")]
        public static readonly AverageCounter ErrorsNintendo = new AverageCounter("CustomAuthErrors_Nintendo");

        #endregion

        #region Methods

        public static void IncrementHttpRequest(long time)
        {
            HttpRequetsTime.IncrementBy(time);
            HttpRequests.Increment();
        }

        public static void IncrementResultsData()
        {
            ResultsData.Increment();
        }

        public static void IncrementResultsAccepted()
        {
            ResultsAccepted.Increment();
        }

        public static void IncrementResultDenied()
        {
            ResultsDenied.Increment();
        }

        public static void IncrementQueueTimeouts()
        {
            QueueTimeouts.Increment();
        }

        public static void IncrementQueueFullErrors()
        {
            QueueFullErrors.Increment();
        }

        public static void IncrementErrors(ClientAuthenticationType authType)
        {
            Errors.Increment();

            AverageCounter counter;
            switch (authType)
            {
                case ClientAuthenticationType.Custom:
                    counter = ErrorsCustom;
                    break;
                case ClientAuthenticationType.Steam:
                    counter = ErrorsSteam;
                    break;
                case ClientAuthenticationType.Facebook:
                    counter = ErrorsFacebook;
                    break;
                case ClientAuthenticationType.Oculus:
                    counter = ErrorsOculus;
                    break;
                case ClientAuthenticationType.PlayStation:
                    counter = ErrorsPlayStation;
                    break;
                case ClientAuthenticationType.Xbox:
                    counter = ErrorsXBox;
                    break;
                case ClientAuthenticationType.PlayerIo:
                    counter = ErrorsPlayerIo;
                    break;
                case ClientAuthenticationType.Jwt:
                    counter = ErrorsJwt;
                    break;
                case ClientAuthenticationType.Viveport:
                    counter = ErrorsViveport;
                    break;
                case ClientAuthenticationType.Nintendo:
                    counter = ErrorsNintendo;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("authType", authType, null);
            }
            if (counter != null)
            {
                counter.Increment();
            }
        }

        public static void IncrementHttpErrors(ClientAuthenticationType authType)
        {
            HttpErrors.Increment();
            AverageCounter counter;
            switch (authType)
            {
                case ClientAuthenticationType.Custom:
                    counter = HttpErrorsCustom;
                    break;
                case ClientAuthenticationType.Steam:
                    counter = HttpErrorsSteam;
                    break;
                case ClientAuthenticationType.Facebook:
                    counter = HttpErrorsFacebook;
                    break;
                case ClientAuthenticationType.Oculus:
                    counter = HttpErrorsOculus;
                    break;
                case ClientAuthenticationType.PlayStation:
                    counter = HttpErrorsPlayStation;
                    break;
                case ClientAuthenticationType.Xbox:
                    counter = HttpErrorsXBox;
                    break;
                case ClientAuthenticationType.Viveport:
                    counter = HttpErrorsViveport;
                    break;
//                case ClientAuthenticationType.Nintendo:
//                    counter = HttpErrorsNintendo;
//                    break;
                default:
                    throw new ArgumentOutOfRangeException("authType", authType, null);
            }
            if (counter != null)
            {
                counter.Increment();
            }
        }

        public static void IncrementHttpTimeouts(ClientAuthenticationType authType)
        {
            HttpTimeouts.Increment();

            AverageCounter counter;
            switch (authType)
            {
                case ClientAuthenticationType.Custom:
                    counter = HttpTimeoutsCustom;
                    break;
                case ClientAuthenticationType.Steam:
                    counter = HttpTimeoutsSteam;
                    break;
                case ClientAuthenticationType.Facebook:
                    counter = HttpTimeoutsFacebook;
                    break;
                case ClientAuthenticationType.Oculus:
                    counter = HttpTimeoutsOculus;
                    break;
                case ClientAuthenticationType.PlayStation:
                    counter = HttpTimeoutsPlayStation;
                    break;
                case ClientAuthenticationType.Xbox:
                    counter = HttpTimeoutsXBox;
                    break;
                case ClientAuthenticationType.Viveport:
                    counter = HttpTimeoutsViveport;
                    break;
//                case ClientAuthenticationType.Nintendo:
//                    counter = HttpTimeoutsNintendo;
//                    break;
                default:
                    throw new ArgumentOutOfRangeException("authType", authType, null);
            }
            if (counter != null)
            {
                counter.Increment();
            }
        }
        #endregion

    }
}
