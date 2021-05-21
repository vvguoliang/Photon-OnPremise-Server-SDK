using Photon.Hive.Operations;
using System;
using System.Collections;
using System.Linq;

namespace Photon.Hive.Common
{
    public class WellKnownProperties
    {
        public bool? IsOpen { get; set; }
        public bool? IsVisible { get; set; }
        public byte? MaxPlayer { get; set; }
        public int? MasterClientId { get; set; }
        public int? PlayerTTL { get; set; }
        public int? EmptyRoomTTL { get; set; }
        public string[] ExpectedUsers { get; set; }
        public object[] LobbyProperties { get; set; }


        public bool TryGetProperties(Hashtable propertyTable, out string debugMessage)
        {
            if (propertyTable == null)
            {
                debugMessage = "Property table is null";
                return false;
            }

            object value;
            byte? maxPlayer;
            bool? isOpen;
            bool? isVisible;
            debugMessage = null;
            if (!TryGetProperties(propertyTable, out maxPlayer, out isOpen, out isVisible, out debugMessage))
            {
                return false;
            }

            int? masterClientId = null;
            if (GameParameterReader.TryReadGameParameter(propertyTable, GameParameter.MasterClientId, out value))
            {
                if (value != null)
                {
                    if (value is int == false)
                    {
                        debugMessage = GetInvalidGamePropertyTypeMessage(GameParameter.MasterClientId, typeof (int), value);
                        return false;
                    }
                    masterClientId = (int)value;
                }
            }

            int? playerTTL;
            if (!GameParameterReader.TryReadIntParameter(propertyTable, GameParameter.PlayerTTL, out playerTTL, out value))
            {
                debugMessage = GetInvalidGamePropertyTypeMessage(GameParameter.PlayerTTL, typeof(int), value);
                return false;
            }

            int? emptyRoomTTL;
            if (!GameParameterReader.TryReadIntParameter(propertyTable, GameParameter.EmptyRoomTTL, out emptyRoomTTL, out value))
            {
                debugMessage = GetInvalidGamePropertyTypeMessage(GameParameter.EmptyRoomTTL, typeof(int), value);
                return false;
            }

            string[] expectedUsers = null;
            if (GameParameterReader.TryReadGameParameter(propertyTable, GameParameter.ExpectedUsers, out value))
            {
                if (value != null)
                {
                    if (value is string[] == false)
                    {
                        debugMessage = GetInvalidGamePropertyTypeMessage(GameParameter.ExpectedUsers, typeof(string[]), value);
                        return false;
                    }
                    expectedUsers = (string[])value;
                }
            }

            object[] properties = null;
            if (GameParameterReader.TryReadGameParameter(propertyTable, GameParameter.LobbyProperties, out value))
            {
                if (value != null && value is object[] == false)
                {
                    debugMessage = GetInvalidGamePropertyTypeMessage(GameParameter.LobbyProperties, typeof(object[]), value);
                    return false;
                }

                properties = (object[])value;
            }

            this.IsOpen = isOpen;
            this.IsVisible = isVisible;
            this.MaxPlayer = maxPlayer;
            this.LobbyProperties = RemoveNullsAndDuplicates(properties);
            this.ExpectedUsers = expectedUsers;
            this.PlayerTTL = playerTTL;
            this.EmptyRoomTTL = emptyRoomTTL;
            this.MasterClientId = masterClientId;
            return true;
        }

        public static bool TryGetProperties(Hashtable propertyTable, out byte? maxPlayer, out bool? isOpen, out bool? isVisible, out string debugMessage)
        {
            object value;
            maxPlayer = null;
            isVisible = null;
            isOpen = null;
            debugMessage = null;
            if (GameParameterReader.TryReadByteParameter(propertyTable, GameParameter.MaxPlayers, out maxPlayer, out value) == false)
            {
                debugMessage = GetInvalidGamePropertyTypeMessage(GameParameter.MaxPlayers, typeof(byte), value);
                return false;
            }

            if (GameParameterReader.TryReadBooleanParameter(propertyTable, GameParameter.IsOpen, out isOpen, out value) == false)
            {
                debugMessage = GetInvalidGamePropertyTypeMessage(GameParameter.IsOpen, typeof(bool), value);
                return false;
            }

            if (GameParameterReader.TryReadBooleanParameter(propertyTable, GameParameter.IsVisible, out isVisible, out value) == false)
            {
                debugMessage = GetInvalidGamePropertyTypeMessage(GameParameter.IsVisible, typeof(bool), value);
                return false;
            }

            return true;
        }


        #region Helpers
        private static string GetInvalidGamePropertyTypeMessage(GameParameter parameter, Type expectedType, object value)
        {
            return string.Format(
                "Invalid type for property {0}. Expected type {1} but is {2}", parameter, expectedType, 
                value == null ? "null" : value.GetType().ToString());
        }

        private static object[] RemoveNullsAndDuplicates(object[] propertyKeys)
        {
            if (propertyKeys != null && propertyKeys.Length > 0)
            {
                return propertyKeys.Where(o => o != null).Distinct().ToArray();
            }

            return propertyKeys;
        }

        #endregion
    }
}
