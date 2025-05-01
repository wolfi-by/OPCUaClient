using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OPCUaClient
{
    public enum OtClientState
    {
        Unknown,
        Disconnected,
        Connected,
        Reconnecting
    }
}
