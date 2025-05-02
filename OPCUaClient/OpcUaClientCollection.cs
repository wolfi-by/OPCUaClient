using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;

namespace OPCUaClient
{
    /// <summary>
    ///   This class is used to manage a collection of OPC UA clients.
    /// </summary>
    /// <param name="opcUaComlexTypeSystemSessionBuilder"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="logger"></param>
    public sealed class OpcUaClientCollection(OpcUaComplexTypeSystemSessionBuilder opcUaComlexTypeSystemSessionBuilder,
        ILoggerFactory loggerFactory,
        ILogger<OpcUaClientCollection> logger) : IDisposable
    {
        private readonly OpcUaComplexTypeSystemSessionBuilder _opcUaComlexTypeSystemSessionBuilder = opcUaComlexTypeSystemSessionBuilder;
        private readonly ILoggerFactory _loggerFactory = loggerFactory;
        private readonly ILogger<OpcUaClientCollection> _logger = logger;


        readonly HashSet<IOtClient> _otClients = [];

        public IOtClient? GetOtClient(string ipAddress)
        {
            var client = _otClients.FirstOrDefault(c => c.IPAddress.Contains(ipAddress));
            return client;
        }

        /// <summary>
        /// Get the state of the OPC UA client by IP address.
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        public OtClientState GetOtClientState(string ipAddress)
            => GetOtClient(ipAddress)?.State ?? OtClientState.Unknown;

        public async Task<IOtClient> CreateClientAsync(string sessionName, string connectionString, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Spawning OPC UA client {sessionName} with connection string {connectionString}", sessionName, connectionString);

            var opcUaClient = new OpcUaClient(_opcUaComlexTypeSystemSessionBuilder, _loggerFactory.CreateLogger<OpcUaClient>());

            if (_otClients.TryGetValue(opcUaClient, out var existingClient))
            {
                opcUaClient.Dispose();
                await existingClient.ConnectAsync(sessionName, connectionString, cancellationToken);
                return existingClient;
            }

            await opcUaClient.ConnectAsync(sessionName, connectionString, cancellationToken);

            if (!_otClients.Add(opcUaClient))
            {
                opcUaClient.Dispose();
                throw new InvalidOperationException($"Client already exists.");
            }
            return opcUaClient;
        }

        public async Task RemoveClientAsync(string ipAddress, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Despawning OPC UA client with session name {ipaddress}", ipAddress);

            var otClient=_otClients.FirstOrDefault(c=>c.IPAddress.Contains(ipAddress));

            Debug.Assert(otClient is not null);

            await otClient.DisconnectAsync(cancellationToken);

            var clientCountBeforeRemove = _otClients.Count;
            var removedClientCount=_otClients.RemoveWhere(x=>x.IPAddress.Contains(ipAddress));
            var clientCountAfterRemove = _otClients.Count;

            Debug.Assert(clientCountAfterRemove == clientCountBeforeRemove - removedClientCount);
        }

        public void Dispose()
        {
            foreach(var client in _otClients)
            {
                client.Dispose();
            }
        }
    }
}