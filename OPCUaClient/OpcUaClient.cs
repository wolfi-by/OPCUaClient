using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Client.ComplexTypes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;
using Mapster;
using Microsoft.Extensions.Logging;

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
        public OtClientState State { get; private set; } = OtClientState.Disconnected;
        public async Task ConnectAsync(string sessionName, string connectionString, CancellationToken cancellationToken = default)
        {
            try
            {
                _session = await SessionBuilder.BuildAsync(sessionName, connectionString, cancellationToken);

                _session.KeepAlive += OnKeepAlive;
                _session.SessionClosing += OnSessionClosing;
                _session.SubscriptionsChanged += OnSubscriptionsChanged;
            }
            catch (Exception ex)
            {
                if (OnError is not null)
                {
                    await OnError.Invoke(this, ex);
                }
            }
        }
        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_session is null)
            {
                return;
            }
            _session.KeepAlive -= OnKeepAlive;
            _session.SessionClosing -= OnSessionClosing;
            _session.SubscriptionsChanged -= OnSubscriptionsChanged;
            foreach (var subscription in _session.Subscriptions)
            {
                foreach (var monitoredItem in subscription.MonitoredItems)
                {
                    monitoredItem.DetachNotificationEventHandlers();
                }
            }

            await _session.CloseAsync(CancellationToken.None);

            StateChanged = null;
        }
        public async Task SubscribeAsync<T>(IOtNode node, Action<T> action, CancellationToken cancellationToken = default)
        {
            var subscription = _session?.Subscribe(node, action);
            if (subscription is null)
            {
                return;
            }
            if (!subscription.Created)
            {
                await subscription.CreateAsync(cancellationToken);
            }
        }
        public async Task SubscribeAsync<T>(IOtNode node, Func<T, Task> asyncFunc, CancellationToken cancellationToken = default)
        {
            var subscription = _session?.Subscribe(node, asyncFunc);
            if (subscription is null)
            {
                return;
            }
            if (!subscription.Created)
            {
                await subscription.CreateAsync(cancellationToken);
            }
        }
        public async Task SubscribeAsync<T>(IEnumerable<IOtNode> nodes, Action<T> action, CancellationToken cancellationToken = default)
        {
            var subscription = _session?.Subscribe(nodes, action);
            if (subscription is null)
            {
                return;
            }
            if (!subscription.Created)
            {
                await subscription.CreateAsync(cancellationToken);
            }
        }
        public async Task SubscribeAsync<T>(IOtNode node, Func<T, ValueTask> asyncFunc, CancellationToken cancellationToken = default)
        {
            var subscription = _session?.Subscribe(node, asyncFunc);
            if (subscription is null)
            {
                return;
            }
            if (!subscription.Created)
            {
                await subscription.CreateAsync(cancellationToken);
            }
        }
        public async Task SubscribeAsync<T>(IEnumerable<IOtNode> nodes, Func<T, Task> asyncFunc, CancellationToken cancellationToken = default)
        {
            var subscription = _session?.Subscribe(nodes, asyncFunc);
            if (subscription is null)
            {
                return;
            }
            if (!subscription.Created)
            {
                await subscription.CreateAsync(cancellationToken);
            }
        }
        public async Task SubscribeAsync<T>(IEnumerable<IOtNode> nodes, Func<T, ValueTask> asyncFunc, CancellationToken cancellationToken = default)
        {
            var subscription = _session?.Subscribe(nodes, asyncFunc);
            if (subscription is null)
            {
                return;
            }
            if (!subscription.Created)
            {
                await subscription.CreateAsync(cancellationToken);
            }
        }
        public Task UnsubscribeAsync(IOtNode node, CancellationToken cancellationToken = default)
        {
            _session?.Unsubscribe(node);
            return Task.CompletedTask;
        }
        public async Task WriteAsync<T>(IOtNode node, T message, CancellationToken cancellationToken = default) where T : notnull
        {
            if (_session is null)
            {
                return;
            }

            if (!_cachedTypeMaps.TryGetValue(typeof(T), out DataValue? dataValue))
            {
                DataValue? value = await _session.ReadValueAsync(node.ToNodeId(), cancellationToken);

                if (value is null)
                {
                    return;
                }
                _cachedTypeMaps.Add(typeof(T), value);
                dataValue = value;
            }
            DataValue dv = new();

            if (dataValue.Value is ExtensionObject ext && ext.Body is IEncodeable encodeable)
            {
                dv.Value = message.Adapt(encodeable.GetType());
            }
            else
            {
                dv.Value = message.Adapt(dataValue.Value.GetType());
            }

            WriteValue writeValue = new()
            {
                Handle = DateTimeOffset.UtcNow,
                NodeId = node.ToNodeId(),
                AttributeId = Attributes.Value,
                Value = dv,
                Processed = true,
            };
            var result = await _session.WriteAsync(default, [writeValue], cancellationToken);
        }

        private void OnSessionClosing(object? sender, EventArgs e)
        {
            if (sender is not ISession session)
            {
                Logger.LogWarning("Session closing event received from unknown session.");
                return;
            }
            DisconnectedStateAndInvoke();

            Logger.LogInformation("Session '{sessionName}' closed.", session.SessionName);
        }

        private async void OnKeepAlive(ISession session, KeepAliveEventArgs e)
        {
            Logger.LogDebug("Keep alive signal for session: '{sessionName}' state: '{currentState}'", session.SessionName, e.CurrentState);

            if (!Object.ReferenceEquals(session, _session))
            {
                Logger.LogInformation("Old session '{sessionName}' with ID '{id}' differs from keep alive session '{keepSession}' with ID '{keepID}'. New session state: '{currentState}'", _session?.SessionName, _session?.SessionId, session.SessionName, session.SessionId, e.CurrentState);
                session.KeepAlive -= OnKeepAlive;
                session.SessionClosing -= OnSessionClosing;
                session.SubscriptionsChanged -= OnSubscriptionsChanged;
                return;
            }
            if (ServiceResult.IsGood(e.Status))
            {
                return;
            }

            Logger.LogInformation("Session '{sessionName}' state: '{currentState}'", session.SessionName, e.CurrentState);

            try
            {
                Logger.LogInformation("Trying to recreate session '{session}'", session.SessionName);
                await RecreateSessionAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to recreate session '{s}'. Message: {m}", session.SessionName, ex.Message);
                Logger.LogInformation(ex, "Trying to recreate session '{session}'", session.SessionName);
            }
        }

        bool _isRecreatingSession;
        SessionReconnectHandler? _sessionReconnectHandler;

        private async Task RecreateSessionAsync()
        {
            if (_isRecreatingSession)
            {
                return;
            }

            if (_session is null)
            {
                return;
            }

            ReconnectingStateAndInvoke();

            try
            {
                _isRecreatingSession = true;
                await _semaphore.WaitAsync();

                _sessionReconnectHandler = new();
                _sessionReconnectHandler.BeginReconnect(_session, 5_000, OnReconnectComplete);

                _session.KeepAlive -= OnKeepAlive;
                _session.SessionClosing -= OnSessionClosing;
                _session.SubscriptionsChanged -= OnSubscriptionsChanged;
            }
            catch
            {
                // ignored
            }
            finally
            {
                _isRecreatingSession = false;
                _semaphore.Release();
            }
        }

        private void OnReconnectComplete(object? sender, EventArgs e)
        {
            if (!Object.ReferenceEquals(sender, _sessionReconnectHandler))
            {
                Debug.Assert(false, "Reconnect event from unknown session.");
                return;
            }

            if (sender is not SessionReconnectHandler handler)
            {
                Debug.Assert(false, "Reconnect event from unknown session.");
                return;
            }

            try
            {
                _semaphore.Wait();

                _session = handler.Session;
                var complexTypeSystem = new ComplexTypeSystem(_session);
                complexTypeSystem.Load(false, true, CancellationToken.None).Wait();

                List<Subscription> newSubscriptions = [];
                foreach (var oldSubscription in _session.Subscriptions)
                {
                    newSubscriptions.Add(new Subscription(oldSubscription));
                    oldSubscription.Delete(true);

                }
                _session.RemoveSubscriptions(_session.Subscriptions.ToArray());

                foreach (var newSubscription in newSubscriptions)
                {
                    _session.AddSubscription(newSubscription);
                    newSubscription.Create();
                }

                _session.KeepAlive += OnKeepAlive;
                _session.SessionClosing += OnSessionClosing;
                _session.SubscriptionsChanged += OnSubscriptionsChanged;

                ConnectedStateAndInvoke();
            }

            catch
            {
                if (_session is not null)
                {
                    _session.KeepAlive -= OnKeepAlive;
                    _session.SessionClosing -= OnSessionClosing;
                    _session.SubscriptionsChanged -= OnSubscriptionsChanged;

                    _session.Close();
                    _session.Dispose();
                }
                DisconnectedStateAndInvoke();
                _session = null;
            }
            finally
            {
                _sessionReconnectHandler?.Dispose();
                _sessionReconnectHandler = null;

                _semaphore.Release();
            }
        }

        private void OnSubscriptionsChanged(object? sender, EventArgs e)
        {
            if (_session is null)
            {
                return;
            }

            var pendingChanges = _session.Subscriptions.Where(x => x.ChangesPending);

            foreach (var subscription in pendingChanges)
            {
                if (!subscription.Created)
                {
                    subscription.Create();
                    continue;
                }
                if (subscription.MonitoredItemCount == 0)
                {
                    subscription.Delete(silent: true);
                    continue;
                }
                subscription.ApplyChanges();
            }
        }

        public override bool Equals(object? obj)
        {
            if (obj is not IOtClient client)
            {
                return false;
            }
            return Equals(client);
        }

        public bool Equals(IOtClient? other)
        {
            if (other is not OpcUaClient client)
            {
                return false;
            }
            return Equals(client);
        }
        public bool Equals(OpcUaClient? other)
        {
            if (other is null)
            {
                return false;
            }
            return this.GetHashCode() == other.GetHashCode();
        }

        public override int GetHashCode()
        {
                return _session?.ConfiguredEndpoint.EndpointUrl.GetHashCode() ?? 0;
        }

        void ConnectedStateAndInvoke()
        {
            StateChanged?.Invoke(this, OtClientState.Connected);
            State = OtClientState.Connected;
        }

        void DisconnectedStateAndInvoke()
        {
            StateChanged?.Invoke(this, OtClientState.Disconnected);
            State = OtClientState.Disconnected;
        }

        void ReconnectingStateAndInvoke()
        {
            StateChanged?.Invoke(this, OtClientState.Reconnecting);
            State = OtClientState.Reconnecting;
        }

        public void Dispose()
        {
            if (_session is not null)
            {
                _session.KeepAlive -= OnKeepAlive;
                _session.SessionClosing -= OnSessionClosing;
                _session.SubscriptionsChanged -= OnSubscriptionsChanged;

                foreach (var subscription in _session.Subscriptions)
                {
                    foreach (var item in subscription.MonitoredItems)
                    {
                        item.DetachNotificationEventHandlers();
                    }
                    subscription.Dispose();
                }
            _session.Close();
            }
            _session?.Dispose();
            _semaphore.Dispose();
            _sessionReconnectHandler?.Dispose();
        }

        
    }
}
