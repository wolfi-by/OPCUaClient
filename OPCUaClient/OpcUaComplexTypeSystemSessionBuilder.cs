using Opc.Ua.Client;
using Opc.Ua.Client.ComplexTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OPCUaClient
{
    /// <summary>
    ///    This class is used to create a session with the OPC UA server.
    /// </summary>
    /// <param name="SessionBulder"></param>
    public sealed class OpcUaComplexTypeSystemSessionBuilder(OpcUaSessionBuilder SessionBuilder)
    {
        /// <summary>
        /// Build Client
        /// </summary>
        /// <param name="sessionName"></param>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public ISession Build(string sessionName, string endpoint)
        {
            return BuildAsync(sessionName, endpoint, CancellationToken.None)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
        /// <summary>
        /// Build client
        /// </summary>
        /// <param name="sessionName"></param>
        /// <param name="endpoint"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<ISession> BuildAsync(string sessionName, string endpoint, CancellationToken cancellationToken)
        {
            var session = await SessionBuilder
                 .WithSessionName(sessionName)
                 .WithEndpoint(endpoint)
                 .BuildAsync(cancellationToken);

            var complexTypeSystem = new ComplexTypeSystem(session);
            await complexTypeSystem.Load(false, true, cancellationToken);

            return session;
        }
    }
}
