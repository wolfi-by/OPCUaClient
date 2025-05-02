

using Opc.Ua;
using System;

namespace OPCUaClient
{
    /// <summary>
    ///   This class is used to represent an OPC UA node.
    /// </summary>
    /// <param name="Node"></param>
    public sealed class OpcUaNode(NodeId Node) : IOtNode<NodeId>
    {
        /// <summary>
        ///   The node represented as a NodeId.
        /// </summary>
        public NodeId Node { get; } = Node;
        /// <summary>
        ///  The node represented as a string.
        /// </summary>
        public string Endpoint => Node.ToString();
    }
}
