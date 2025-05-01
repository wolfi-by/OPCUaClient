using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OPCUaClient.old.Exceptions
{
    internal class WriteException : Exception
    {
        public WriteException(string? message) : base(message)
        {
        }
    }
}
