﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.Tools
{
    public enum OperationCode : byte
    {
        Login,
        Register,
        Default,
        SyncPosition,
        syncPlayer
    }
}
