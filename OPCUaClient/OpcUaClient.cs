using Opc.Ua;
using Opc.Ua.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OPCUaClient
{
    internal sealed class OpcUaClient(OpcUaComplexTypeSystemSessionBuilder SessionBuilder,
        ILogger<OpcUaClient> Logger) : IOtClient
    {
        public Func<IOtClient, Exception, Task>? OnError { get; set; }
        public Func<IOtClient, OtClientState, Task>? StateChanged { get; set; }


        ISession? _session;
        readonly SemaphoreSlim _semaphore = new(1);
        readonly Dictionary<Type, DataValue> _cachedTypeMaps = [];
        public string Name => _session?.SessionName ?? string.Empty;
        public string IPAddress => _session?.Endpoint.EndpointUrl ?? string.Empty;









        public OtClientState State => throw new NotImplementedException();


        public Task ConnectAsync(string sessionName, string connectionString, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Task SubscribeAsync<T>(IOtNode node, Action<T> action, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task SubscribeAsync<T>(IOtNode node, Func<T, Task> asyncFunc, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task SubscribeAsync<T>(IOtNode node, Func<T, ValueTask> asyncFunc, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task SubscribeAsync<T>(IEnumerable<IOtNode> nodes, Action<T> action, char CancellationToken = '\0')
        {
            throw new NotImplementedException();
        }

        public Task SubscribeAsync<T>(IEnumerable<IOtNode> nodes, Func<T, Task> asyncFunc, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task SubscribeAsync<T>(IEnumerable<IOtNode> nodes, Func<T, ValueTask> asyncFunc, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task UnsubscribeAsync(IOtNode node, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task WriteAsync<T>(IOtNode node, T message, CancellationToken cancellationToken = default) where T : notnull
        {
            throw new NotImplementedException();
        }
    }
}
