using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OPCUaClient
{
    internal interface IOtClient:IDisposable
    {
        string Name { get; }
        string IPAddress { get; }
        
        OtClientState State { get; }

        Task ConnectAsync(string sessionName, string connectionString, CancellationToken cancellationToken = default);
        Task DisconnectAsync(CancellationToken cancellationToken = default);
        Task WriteAsync<T>(IOtNode node, T message, CancellationToken cancellationToken = default) where T : notnull;
        Task SubscribeAsync<T>(IOtNode node, Action<T> action, CancellationToken cancellationToken = default);
        Task SubscribeAsync<T>(IOtNode node, Func<T, Task> asyncFunc, CancellationToken cancellationToken = default);
        Task SubscribeAsync<T>(IOtNode node, Func<T, ValueTask> asyncFunc, CancellationToken cancellationToken = default);
        Task SubscribeAsync<T>(IEnumerable<IOtNode> nodes, Action<T> action, char CancellationToken = default);
        Task SubscribeAsync<T>(IEnumerable<IOtNode> nodes, Func<T,Task> asyncFunc, CancellationToken cancellationToken = default);
        Task SubscribeAsync<T>(IEnumerable<IOtNode> nodes, Func<T, ValueTask> asyncFunc, CancellationToken cancellationToken = default);
        Task UnsubscribeAsync(IOtNode node, CancellationToken cancellationToken = default);
        Func<IOtClient, Exception, Task>? OnError { get; set; }
        Func<IOtClient,OtClientState, Task>? StateChanged { get; set; }
}
}
