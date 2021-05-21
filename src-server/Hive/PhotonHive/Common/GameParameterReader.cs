// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GameParameterReader.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.Hive.Common
{
    using System;
    using System.Collections;

    using Photon.Hive.Operations;

    /// <summary>
    /// Provides methods to read build in game properties from a hashtable.
    /// </summary>
    /// <remarks>
    /// Build in game properties in the load balancing project are stored as byte values. 
    /// Because some protocols used by photon (Flash, WebSockets) does not support byte values
    /// the properties will also be searched in the hashtable using there int representation.
    /// If an int representation is found it will be converted to the byte representation of 
    /// the game property.
    /// </remarks>
    public static class GameParameterReader
    {
        #region Public Methods

        public static bool TryReadBooleanParameter(Hashtable hashtable, GameParameter paramter, out bool? result, out object value)
        {
            result = null;

            if (!TryReadGameParameter(hashtable, paramter, out value))
            {
                return true;
            }

            if (value is bool)
            {
                result = (bool)value;
                return true;
            }

            return false;
        }

        public static bool TryReadByteParameter(Hashtable hashtable, GameParameter paramter, out byte? result, out object value)
        {
            result = null;

            if (!TryReadGameParameter(hashtable, paramter, out value))
            {
                return true;
            }

            if (value is byte)
            {
                result = (byte)value;
                return true;
            }

            if (value is int)
            {
                result = (byte)(int)value;
                hashtable[(byte)paramter] = result;
                return true;
            }

            if (value is double)
            {
                result = (byte)(double)value;
                hashtable[(byte)paramter] = result;
                return true;
            }

            return false;
        }

        public static bool TryReadIntParameter(Hashtable hashtable, GameParameter paramter, out int? result, out object value)
        {
            result = null;

            if (!TryReadGameParameter(hashtable, paramter, out value))
            {
                return true;
            }

            if (value is byte)
            {
                result = (byte)value;
                hashtable[(byte)paramter] = result;
                return true;
            }

            if (value is int)
            {
                result = (int)value;
                return true;
            }

            if (value is double)
            {
                result = (int)(double)value;
                hashtable[(byte)paramter] = result;
                return true;
            }

            return false;
        }

        public static bool TryReadGameParameter(Hashtable hashtable, GameParameter paramter, out object result)
        {
            var byteKey = (byte)paramter;
            if (hashtable.ContainsKey(byteKey))
            {
                result = hashtable[byteKey];
                return true;
            }

            var intKey = (int)paramter;
            if (hashtable.ContainsKey(intKey))
            {
                result = hashtable[intKey];
                hashtable.Remove(intKey);
                hashtable[byteKey] = result;
                return true;
            }

            result = null;
            return false;
        }

        #endregion

    }
}