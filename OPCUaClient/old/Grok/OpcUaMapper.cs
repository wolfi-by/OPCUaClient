using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OPCUaClient.old.Grok
{
    public static class OpcUaMapper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="extensionObject"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        // Mappt ein OPC UA ExtensionObject auf eine C#-Klasse
        public static T MapFromOpcUa<T>(ExtensionObject extensionObject) where T : class, new()
        {
            if (extensionObject == null) return default!;

            T result = new T();
            var decodedObject = extensionObject.Body as VariantCollection;

            if (decodedObject == null)
            {
                throw new Exception("Konnte das ExtensionObject nicht dekodieren.");
            }

            // Hole die Felder der Zielklasse
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // Mappe die Werte aus der VariantCollection auf die Felder
            for (int i = 0; i < properties.Length && i < decodedObject.Count; i++)
            {
                var property = properties[i];
                var opcValue = decodedObject[i].Value;

                // Konvertiere den Wert in den richtigen Typ
                if (opcValue != null && property.PropertyType.IsAssignableFrom(opcValue.GetType()))
                {
                    property.SetValue(result, opcValue);
                }
                else if (opcValue is ExtensionObject nestedExtensionObject)
                {
                    // Rekursives Mapping für verschachtelte Strukturen
                    var nestedType = property.PropertyType;
                    var nestedInstance = Activator.CreateInstance(nestedType);
                    var mappedNested = typeof(OpcUaMapper)
                        ?.GetMethod(nameof(MapFromOpcUa))
                        ?.MakeGenericMethod(nestedType)
                        .Invoke(null, new object[] { nestedExtensionObject });
                    property.SetValue(result, mappedNested);
                }
                else if (opcValue is Array array)
                {
                    // Unterstützung für Arrays
                    property.SetValue(result, array);
                }
            }

            return result;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="dataTypeId"></param>
        /// <returns></returns>
        // Konvertiert eine C#-Klasse in ein OPC UA ExtensionObject
        public static ExtensionObject MapToOpcUa<T>(T source, NodeId dataTypeId) where T : class
        {
            if (source == null) return default!;

            // Hole die Felder der Quellklasse
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var variantCollection = new VariantCollection();

            // Mappe die Felder in eine VariantCollection
            foreach (var property in properties)
            {
                var value = property.GetValue(source);

                if (value == null)
                {
                    variantCollection.Add(Variant.Null);
                }
                else if (value.GetType().IsClass && value.GetType() != typeof(string))
                {
                    // Rekursives Mapping für verschachtelte Strukturen
                    var nestedDataTypeId = new NodeId("ns=2;s=" + value.GetType().Name); // Passe die NodeId an
                    var nestedExtensionObject = MapToOpcUa(value, nestedDataTypeId);
                    variantCollection.Add(new Variant(nestedExtensionObject));
                }
                else if (value is Array array)
                {
                    // Unterstützung für Arrays
                    variantCollection.Add(new Variant(array));
                }
                else
                {
                    // Elementarer Datentyp
                    variantCollection.Add(new Variant(value));
                }
            }

            // Erstelle ein ExtensionObject
            return new ExtensionObject(dataTypeId, variantCollection);
        }
    }
}
