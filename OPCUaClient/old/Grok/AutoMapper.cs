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
    public static class AutoMapper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="opcData"></param>
        /// <param name="session"></param>
        /// <param name="fieldDataTypeIds"></param>
        /// <returns></returns>
        public static async Task<T> MapToClass<T>(ExtensionObject opcData, Session session, Dictionary<string, NodeId> fieldDataTypeIds) where T : class, new()
        {
            if (opcData == null) return default!;

            // Dekodiere das ExtensionObject in die Zielklasse
            return await GenericStructureParser.DecodeFromExtensionObject<T>(opcData, session, fieldDataTypeIds);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="dataTypeId"></param>
        /// <param name="session"></param>
        /// <param name="fieldDataTypeIds"></param>
        /// <returns></returns>
        public static async Task<ExtensionObject> MapToOpcStructure<T>(T source, NodeId dataTypeId, Session session, Dictionary<string, NodeId> fieldDataTypeIds) where T : class, new()
        {
            if (source == null) return default!;

            // Konvertiere die C#-Klasse in ein ExtensionObject
            return await GenericStructureParser.EncodeToExtensionObject(source, dataTypeId, session, fieldDataTypeIds);
        }
    }
}
