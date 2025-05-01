using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OPCUaClient
{
    /// <summary>
    ///   This interface is used to represent a node in the OPC UA server.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IOtNode<T> : IOtNode
    {
        /// <summary>
        /// The generic node
        /// </summary>
        T Node { get; }

    }

    /// <summary>
    ///   This interface is used to represent a node in the OPC UA server.
    /// </summary>
    public interface IOtNode
    {
        /// <summary>
        /// The node represended as string
        /// </summary>
        string Endpoint { get; }
    }
}
