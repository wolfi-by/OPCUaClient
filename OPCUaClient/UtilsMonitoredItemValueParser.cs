using Mapster;
using Opc.Ua;
using Opc.Ua.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OPCUaClient
{
    internal static class UtilsMonitoredItemValueParser
    {

        internal static T? ToValue<T>(this MonitoredItemNotificationEventArgs e)
        {
            if (e.NotificationValue is not MonitoredItemNotification notification)
            {
                return default;
            }
            return notification.Value.ToValue<T>();
        }

        internal static T? ToValue<T>(this DataValue dataValue)
        {
            if (dataValue.Value is not ExtensionObject extensionObject)
            {
                return default;
            }
            return extensionObject.GetValue<T>();
        }

        internal static T GetValue<T>(this ExtensionObject extensionObject)
        => extensionObject.Body.Adapt<T>();
    }
}
