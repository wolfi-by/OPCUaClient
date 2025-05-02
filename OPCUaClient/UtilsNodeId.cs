using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OPCUaClient
{
    internal static class UtilsNodeId
    {
        internal static NodeId ToNodeId(this IOtNode node) => new NodeId(node.Endpoint);
        internal static IOtNode ToOpcUaNode(this NodeId nodeId) => new OpcUaNode(nodeId);

      
    }
}
