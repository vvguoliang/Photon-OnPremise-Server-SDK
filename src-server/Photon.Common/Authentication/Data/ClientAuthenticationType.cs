﻿
namespace Photon.Common.Authentication.Data
{
    public enum ClientAuthenticationType : byte
    {
        Custom = 0,
        Steam = 1,
        Facebook = 2,
        Oculus = 3,
        PlayStation = 4,
        Xbox = 5,
        PlayerIo = 8,
        Jwt = 9,
        Viveport = 10,
        Nintendo = 11,
    }
}
