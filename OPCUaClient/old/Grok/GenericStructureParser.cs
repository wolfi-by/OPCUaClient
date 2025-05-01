using Ardalis.Result;
using Opc.Ua;
using Opc.Ua.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OPCUaClient.old.Grok
{
    /// <summary>
    /// 
    /// </summary>
    public static class GenericStructureParser
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="structure"></param>
        /// <param name="dataTypeId"></param>
        /// <param name="session"></param>
        /// <param name="fieldDataTypeIds"></param>
        /// <returns></returns>
        public static async Task<ExtensionObject> EncodeToExtensionObject(object structure, NodeId dataTypeId, Session session, Dictionary<string, NodeId> fieldDataTypeIds)
        {
            if (structure == null) return default!;

            // Erstelle einen Encoder für die Struktur
            var encoder = new BinaryEncoder(session.MessageContext);
            await EncodeAsync(encoder, structure, session, fieldDataTypeIds);

            // Erstelle ein ExtensionObject mit dem encodierten Inhalt
            return new ExtensionObject(dataTypeId, encoder.CloseAndReturnBuffer());
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="encoder"></param>
        /// <param name="structure"></param>
        /// <param name="session"></param>
        /// <param name="fieldDataTypeIds"></param>
        /// <exception cref="Exception"></exception>
        public static async Task EncodeAsync(IEncoder encoder, object structure, Session session, Dictionary<string, NodeId> fieldDataTypeIds)
        {
            if (structure == null) return;

            // Hole alle öffentlichen Eigenschaften der Klasse
            var properties = structure.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var value = property.GetValue(structure);
                var propertyType = property.PropertyType;
                var propertyName = property.Name;

                // Unterstütze elementare Datentypen
                if (propertyType == typeof(int))
                    encoder.WriteInt32(propertyName, (int)(value ?? 0));
                else if (propertyType == typeof(double))
                    encoder.WriteDouble(propertyName, (double)(value ?? 0.0));
                else if (propertyType == typeof(float))
                    encoder.WriteFloat(propertyName, (float)(value ?? 0.0f));
                else if (propertyType == typeof(string))
                    encoder.WriteString(propertyName, (string)value!);
                else if (propertyType == typeof(bool))
                    encoder.WriteBoolean(propertyName, (bool)(value ?? false));
                else if (propertyType.IsClass && propertyType != typeof(string))
                {
                    // Verschachtelte Struktur
                    NodeId nestedDataTypeId = fieldDataTypeIds.ContainsKey(propertyName) ? fieldDataTypeIds[propertyName] : NodeId.Null;
                    if (nestedDataTypeId != NodeId.Null)
                    {
                        // Lese die DataTypeDefinition der Unterstruktur
                        var (subDataTypeId, subFieldDataTypeIds) = GetDataTypeInfo(session, nestedDataTypeId).GetAwaiter().GetResult();
                        var nestedExtensionObject =await EncodeToExtensionObject(value!, subDataTypeId, session, subFieldDataTypeIds);
                        encoder.WriteExtensionObject(propertyName, nestedExtensionObject);
                    }
                    else
                    {
                        throw new Exception($"DataTypeId für das Feld {propertyName} konnte nicht ermittelt werden.");
                    }
                }
                else if (propertyType.IsArray)
                {
                    // Unterstützung für Arrays
                    var array = value as Array;
                    if (array != null)
                    {
                        encoder.WriteVariant(propertyName, new Variant(array));
                    }
                }
                else
                {
                    throw new Exception($"Nicht unterstützter Datentyp: {propertyType.Name} für Eigenschaft {propertyName}");
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="session"></param>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task<(NodeId DataTypeId, Dictionary<string, NodeId> FieldDataTypeIds)> GetDataTypeInfo(Session session, NodeId nodeId)
        {
            // Lese die DataTypeId der Node
            var nodesToRead = new ReadValueIdCollection
    {
        new ReadValueId
        {
            NodeId = nodeId,
            AttributeId = Attributes.DataType // Attribut für den Datentyp
        }
    };

            session.Read(null, 0, TimestampsToReturn.Neither, nodesToRead, out DataValueCollection results, out DiagnosticInfoCollection diagnostics);

            if (!StatusCode.IsGood(results[0].StatusCode))
            {
                throw new Exception($"Fehler beim Auslesen des Datentyps der Node {nodeId}: {results[0].StatusCode}");
            }

            var dataTypeId = results[0].Value as NodeId;
            if (dataTypeId == null)
            {
                throw new Exception($"Datentyp der Node {nodeId} konnte nicht ermittelt werden.");
            }

            // Lese die DataTypeDefinition, um die Datentypen der Felder zu ermitteln
            var fieldDataTypeIds = new Dictionary<string, NodeId>();
            nodesToRead = new ReadValueIdCollection
    {
        new ReadValueId
        {
            NodeId = dataTypeId,
            AttributeId = Attributes.DataTypeDefinition
        }
    };

            session.Read(null, 0, TimestampsToReturn.Neither, nodesToRead, out results, out diagnostics);

            if (StatusCode.IsGood(results[0].StatusCode) && results[0].Value is ExtensionObject definitionObject)
            {
                var dataTypeDefinition = ExtensionObject.ToEncodeable(definitionObject) as StructureDefinition;
                if (dataTypeDefinition != null)
                {
                    foreach (var field in dataTypeDefinition.Fields)
                    {
                        fieldDataTypeIds[field.Name] = field.DataType;
                    }
                }
            }

            return (dataTypeId, fieldDataTypeIds);
        }
        public static async Task<T> DecodeFromExtensionObject<T>(ExtensionObject extensionObject, Session session, Dictionary<string, NodeId> fieldDataTypeIds) where T : class, new()
        {
            if (extensionObject == null) return null;

            T result = new T();
            if (extensionObject.Body is byte[] body)
            {
                // Erstelle einen Decoder für die Struktur
                var decoder = new BinaryDecoder(body, null);
                await Decode(decoder, result, session, fieldDataTypeIds);
            }
            return result;
        }

        public static async Task Decode(IDecoder decoder, object structure, Session session, Dictionary<string, NodeId> fieldDataTypeIds)
        {
            if (structure == null) return;

            // Hole alle öffentlichen Eigenschaften der Klasse
            var properties = structure.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var propertyType = property.PropertyType;
                var propertyName = property.Name;

                // Unterstütze elementare Datentypen
                if (propertyType == typeof(int))
                    property.SetValue(structure, decoder.ReadInt32(propertyName));
                else if (propertyType == typeof(double))
                    property.SetValue(structure, decoder.ReadDouble(propertyName));
                else if (propertyType == typeof(float))
                    property.SetValue(structure, decoder.ReadFloat(propertyName));
                else if (propertyType == typeof(string))
                    property.SetValue(structure, decoder.ReadString(propertyName));
                else if (propertyType == typeof(bool))
                    property.SetValue(structure, decoder.ReadBoolean(propertyName));
                else if (propertyType.IsClass && propertyType != typeof(string))
                {
                    // Verschachtelte Struktur
                    var extensionObject = decoder.ReadExtensionObject(propertyName);
                    if (extensionObject != null)
                    {
                        NodeId nestedDataTypeId = fieldDataTypeIds.ContainsKey(propertyName) ? fieldDataTypeIds[propertyName] : NodeId.Null;
                        if (nestedDataTypeId != NodeId.Null)
                        {
                            // Lese die DataTypeDefinition der Unterstruktur
                            var (subDataTypeId, subFieldDataTypeIds) = await GetDataTypeInfo(session, nestedDataTypeId);
                            var nestedObject = Activator.CreateInstance(propertyType);
                            if (extensionObject.Body is byte[] body)
                            {
                                var nestedDecoder = new BinaryDecoder(body, null);
                                await Decode(nestedDecoder, nestedObject, session, subFieldDataTypeIds);
                            }
                            property.SetValue(structure, nestedObject);
                        }
                        else
                        {
                            throw new Exception($"DataTypeId für das Feld {propertyName} konnte nicht ermittelt werden.");
                        }
                    }
                }
                else if (propertyType.IsArray)
                {
                    // Unterstützung für Arrays
                    var variant = decoder.ReadVariant(propertyName);
                    var array = variant.Value as Array;
                    if (array != null)
                    {
                        property.SetValue(structure, array);
                    }
                }
                else
                {
                    throw new Exception($"Nicht unterstützter Datentyp: {propertyType.Name} für Eigenschaft {propertyName}");
                }
            }
        }

       


    }
}
