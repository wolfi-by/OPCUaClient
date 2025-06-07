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

namespace OPCUaClient;
public class MyOpcUaClient
{
    private readonly string endpointUrl;
    private Session session;
    private readonly ApplicationConfiguration config;

    public MyOpcUaClient(string endpointUrl)
    {
        this.endpointUrl = endpointUrl;

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
        try
        {
            // Endpoint auswählen
            var endpointDescription = CoreClientUtils.SelectEndpoint(endpointUrl, useSecurity: false);

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

            await session.LoadDataTypeSystem();

            Console.WriteLine("Verbindung zum OPC UA Server hergestellt.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Verbinden: {ex.Message}");
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        if (session != null)
        {
            await session.CloseAsync();
            session.Dispose();
            session = null;
            Console.WriteLine("Verbindung zum OPC UA Server geschlossen.");
        }
    }

    // Generische Lesemethode
    public T ReadNode<T>(string nodeId)
    {
        if (session == null)
            throw new InvalidOperationException("Client ist nicht verbunden.");

        try
        {
            var node = NodeId.Parse(nodeId);
            if (!typeof(T).IsClass)
            {
                return ReadNodeValue<T>(node);
            }
            return ReadNodeClass<T>(node);



        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Lesen von Node {nodeId}: {ex.Message}");
            throw;
        }
        return default!;
    }

    private T ReadNodeClass<T>(NodeId node)
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
        session.Browse(null, null, 0, browseDescs, out BrowseResultCollection results, out DiagnosticInfoCollection diagnosticInfos);

        if (results == null || results.Count != 1)
        {
            return default!;
        }


        T result = Activator.CreateInstance<T>();
        foreach (var reference in results[0].References)
        {
            var variableName = reference.DisplayName.Text;
            // Die NodeId der referenzierten Variable abrufen
            NodeId variableNodeId = NodeId.Parse(reference.NodeId.ToString());

            var property = result!.GetType().GetProperty(variableName);
            if (property!=null&&property.CanWrite)
            {
                
                if (property.GetType().IsClass)
                {
                    property.SetValue(result,ReadNodeValue<object>(variableNodeId),null);
                    continue;
                }
                property.SetValue(result, ReadNodeValue<object>(variableNodeId), null);
            }
            
        }
        return result;
    }

    private T ReadNodeValue<T>(NodeId node)
    {
        try
        {
            DataValue dataValue = session.ReadValue(node);

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
    public void WriteNode<T>(string nodeId, T value)
    {
        if (session == null)
            throw new InvalidOperationException("Client ist nicht verbunden.");

        try
        {
            var node = NodeId.Parse(nodeId);
            var writeValue = new WriteValue
            {
                NodeId = node,
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(value))
            };

            var writeValues = new WriteValueCollection { writeValue };
            session.Write(null, writeValues, out StatusCodeCollection results, out DiagnosticInfoCollection diagnosticInfos);

            if (!StatusCode.IsGood(results[0]))
            {
                throw new Exception($"Fehler beim Schreiben auf Node {nodeId}: {results[0].ToString()}");
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
}

// Beispielprogramm
class Program
{

}