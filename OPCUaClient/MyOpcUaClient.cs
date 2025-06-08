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
using Ardalis.Result;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace OPCUaClient;
public class MyOpcUaClient : IDisposable
{
    private readonly string endpointUrl;
    private ISession session;
    private SessionReconnectHandler reconnectHandler;
    private readonly ApplicationConfiguration config;
    private readonly IOptions<MyOpcUaClientOptions> _options;
    private readonly ILogger<MyOpcUaClient> _logger;

    public MyOpcUaClient(IOptions<MyOpcUaClientOptions> options, ILogger<MyOpcUaClient> logger)
    {
        _options = options;
        _logger = logger;

        // Grundlegende Konfiguration für den Client
        config = new ApplicationConfiguration
        {
            ApplicationName = "OpcUaClient",
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier(),
                TrustedPeerCertificates = new CertificateTrustList(),
                TrustedIssuerCertificates = new CertificateTrustList(),
                RejectedCertificateStore = new CertificateTrustList(),
                AutoAcceptUntrustedCertificates = true // Nur für Testzwecke
            },
            TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
        };
    }

    public async Task ConnectAsync()
    {
        if (session != null && session.Connected)
        {
            return;
        }
        try
        {
            // Endpoint auswählen

            // Endpoint auswählen mit ApplicationConfiguration
            var endpointDescription = CoreClientUtils.SelectEndpoint(config, _options.Value.endpointUrl, useSecurity: false);
            // Session erstellen
            session = await Session.Create(
                configuration: config,
                endpoint: new ConfiguredEndpoint(null, endpointDescription),
                updateBeforeConnect: false,
                checkDomain: false,
                sessionName: config.ApplicationName,
                sessionTimeout: 60000,
                identity: new UserIdentity(new AnonymousIdentityToken()),
                preferredLocales: null);

            session.KeepAlive += SessionKeepAliveHandler;


            await session.LoadDataTypeSystem();

            Console.WriteLine("Verbindung zum OPC UA Server hergestellt.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Verbinden: {ex.Message}");
            throw;
        }
    }

    private void SessionKeepAliveHandler(ISession sender, KeepAliveEventArgs e)
    {
        if (e.Status != null && ServiceResult.IsNotGood(e.Status))
        {
            Console.WriteLine("{0} {1}/{2}", e.Status, sender.OutstandingRequestCount, sender.DefunctRequestCount);

            if (reconnectHandler == null)
            {
                Console.WriteLine("--- RECONNECTING ---");
                reconnectHandler = new SessionReconnectHandler();
                reconnectHandler.BeginReconnect(sender, _options.Value.ReconnectPeriod * 1000, Client_ReconnectComplete);
            }
        }
    }

    private void Client_ReconnectComplete(object? sender, EventArgs e)
    {
        if (!Object.ReferenceEquals(sender, reconnectHandler))
        {
            return;
        }

        session = reconnectHandler.Session;
        reconnectHandler.Dispose();
        reconnectHandler = null!;
        Console.WriteLine("--- RECONNECTED ---");
    }

    public async Task DisconnectAsync()
    {
        if (session != null)
        {
            await session.CloseAsync();
            session?.Dispose();
            session = null!;
            Console.WriteLine("Verbindung zum OPC UA Server geschlossen.");
        }
    }

    // Generische Lesemethode
    public async Task<T> ReadNodeAsync<T>(string nodeId, CancellationToken ct = default)
    {
        if (session == null)
            throw new InvalidOperationException("Client ist nicht verbunden.");

        try
        {
            var node = NodeId.Parse(nodeId);
            if (!typeof(T).IsClass || typeof(T).IsArray)
            {
                return (await ReadNodeValueAsync<T>(node, ct));
            }

            return (await ReadNodeClassAsync<T>(node, ct));



        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Lesen von Node {nodeId}: {ex.Message}");
            throw;
        }
        return default!;
    }

    private async Task<T> ReadNodeClassAsync<T>(NodeId node, CancellationToken ct)
    {
        BrowseDescription browseDesc = new BrowseDescription
        {
            NodeId = node, // Ersetze mit deiner Node-ID
            BrowseDirection = BrowseDirection.Forward,
            ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
            IncludeSubtypes = true,
            NodeClassMask = (uint)NodeClass.Variable | (uint)NodeClass.Object,
            ResultMask = (uint)BrowseResultMask.All
        };
        BrowseDescriptionCollection browseDescs = new BrowseDescriptionCollection { browseDesc };
        var asyncresult = await session.BrowseAsync(null, null, 0, browseDescs, ct);
        if (asyncresult.Results == null || asyncresult.Results.Count != 1)
        {
            return default!;
        }


        T result = Activator.CreateInstance<T>();
        //foreach (var reference in results[0].References)
        foreach (var reference in asyncresult.Results[0].References)
        {
            var variableName = reference.DisplayName.Text;
            // Die NodeId der referenzierten Variable abrufen
            NodeId variableNodeId = NodeId.Parse(reference.NodeId.ToString());

            var property = result!.GetType().GetProperty(variableName);
            if (property != null && property.CanWrite)
            {

                if (property.GetType().IsClass)
                {
                    property.SetValue(result, await ReadNodeValueAsync<object>(variableNodeId, ct), null);
                    continue;
                }
                property.SetValue(result, await ReadNodeValueAsync<object>(variableNodeId, ct), null);
            }

        }
        return result;
    }

    private async Task<T> ReadNodeValueAsync<T>(NodeId node, CancellationToken ct)
    {
        try
        {
            DataValue dataValue = await session.ReadValueAsync(node, ct);

            if (StatusCode.IsGood(dataValue.StatusCode) && dataValue.Value != null)
            {
                if (typeof(T).IsAssignableFrom(dataValue.Value.GetType()))
                {
                    return (T)dataValue.Value;
                }
                else
                {
                    return (T)Convert.ChangeType(dataValue.Value, typeof(T));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Lesen von Node {node}: {ex.Message}");
            throw;
        }
        return default!;
    }


    // Generische Schreibmethode
    public async Task WriteNodeAsync<T>(string nodeId, T value, CancellationToken ct = default)
    {
        if (session == null)
            throw new InvalidOperationException("Client ist nicht verbunden.");

        try
        {
            var node = NodeId.Parse(nodeId);
            if (!typeof(T).IsClass || typeof(T).IsArray)
            {
                await WriteNodeValueAsync(nodeId, value, ct);
                return;
            }
            await WriteNodeClassAsync(nodeId, value, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Schreiben auf Node {nodeId}: {ex.Message}");
            throw;
        }

    }

    private async Task WriteNodeClassAsync<T>(string nodeId, T? value, CancellationToken ct)
    {
        foreach (var property in value!.GetType().GetProperties())
        {

            var propertyValue = property.GetValue(value);
            if (propertyValue != null)
            {
                var childNodeId = $"{nodeId}.{property.Name}";
                await WriteNodeValueAsync(childNodeId, propertyValue, ct);
            }
        }
    }


    private async Task WriteNodeValueAsync<T>(string nodeId, T? value, CancellationToken ct)
    {
        try
        {
            var writeValue = new WriteValue
            {
                NodeId = nodeId,
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(value))
            };

            var writeValues = new WriteValueCollection { writeValue };
            //session.Write(null, writeValues, out StatusCodeCollection results, out DiagnosticInfoCollection diagnosticInfos);
            WriteResponse result = await session.WriteAsync(null, writeValues, ct);

            if (!StatusCode.IsGood(result.Results[0]))
            {
                throw new Exception($"Fehler beim Schreiben auf Node {nodeId}: {result.Results[0].ToString()}");
            }

            Console.WriteLine($"Erfolgreich geschrieben auf Node {nodeId}: {value}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Schreiben auf Node {nodeId}: {ex.Message}");
            throw;
        }
    }

    // Subskriptionsmethode
    public Subscription SubscribeNode(string nodeId, Action<string, object> callback)
    {
        if (session == null)
            throw new InvalidOperationException("Client ist nicht verbunden.");

        try
        {
            // Subskription erstellen
            var subscription = new Subscription(session.DefaultSubscription)
            {
                PublishingInterval = 1000, // 1 Sekunde
                PublishingEnabled = true
            };

            // Monitored Item hinzufügen
            var monitoredItem = new MonitoredItem
            {
                StartNodeId = NodeId.Parse(nodeId),
                AttributeId = Attributes.Value,
                DisplayName = nodeId,
                SamplingInterval = 1000
            };

            // Callback für Änderungen
            monitoredItem.Notification += (item, args) =>
            {
                foreach (DataValue notification in item.DequeueValues())
                {
                    if (notification != null && StatusCode.IsGood(notification.StatusCode))
                    {
                        callback?.Invoke(nodeId, notification.Value);
                    }
                }
            };

            subscription.AddItem(monitoredItem);
            session.AddSubscription(subscription);
            subscription.Create();

            Console.WriteLine($"Subskription für Node {nodeId} erstellt.");
            return subscription;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Erstellen der Subskription für Node {nodeId}: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        if (session != null)
        {
            try
            {
                session.KeepAlive -= SessionKeepAliveHandler;
                session.Close();
                session.Dispose();
                session = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Schließen der OPC UA Session.");
            }
        }
    }
}

