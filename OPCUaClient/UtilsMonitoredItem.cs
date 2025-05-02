using Opc.Ua;
using Opc.Ua.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace OPCUaClient
{
    internal static class UtilsMonitoredItem
    {
        static readonly DataChangeFilter _dataChangeFilter = new()
        {
            Trigger = DataChangeTrigger.StatusValue
        };

        internal static MonitoredItem CreateMonitoredItem<T>(this IOtNode node, Action<T> action)
            => node.ToNodeId().CreateMonitoredItemInternal(action);

        internal static MonitoredItem CreateMonitoredItem<T>(this IOtNode node, Func<T, Task> asyncFunc)
            => node.ToNodeId().CreateMonitoredItemInternal(asyncFunc);

        internal static MonitoredItem CreateMonitoredItem<T>(this IOtNode node, Func<T, ValueTask> asyncFunc)
           => node.ToNodeId().CreateMonitoredItemInternal(asyncFunc);

        internal static MonitoredItem[] CreateMonitoredItems<T>(this IEnumerable<IOtNode> nodes, Action<T> action)
           =>nodes.Select(node=> node.ToNodeId().CreateMonitoredItemInternal(action)).ToArray();

        internal static MonitoredItem[] CreateMonitoredItems<T>(this IEnumerable<IOtNode> nodes, Func<T, Task> asyncFunc)
            => nodes.Select(node => node.ToNodeId().CreateMonitoredItemInternal(asyncFunc)).ToArray();

        internal static MonitoredItem[] CreateMonitoredItems<T>(this IEnumerable<IOtNode> nodes, Func<T, ValueTask> asyncFunc)
          => nodes.Select(node => node.ToNodeId().CreateMonitoredItemInternal(asyncFunc)).ToArray();

        internal static MonitoredItem CreateMonitoredItemInternal<T>(this NodeId nodeId, Action<T> action)
           => nodeId.DefaultMonitoredItem().AppendDelegateToMonitoredItem(action);

        internal static MonitoredItem CreateMonitoredItemInternal<T>(this NodeId nodeId, Func<T, Task> asyncFunc)
           => nodeId.DefaultMonitoredItem().AppendDelegateToMonitoredItem(asyncFunc);

        internal static MonitoredItem CreateMonitoredItemInternal<T>(this NodeId nodeId, Func<T, ValueTask> asyncFunc)
           => nodeId.DefaultMonitoredItem().AppendDelegateToMonitoredItem(asyncFunc);

        internal static MonitoredItem AppendDelegateToMonitoredItem<T>(this MonitoredItem monitoredItem, Action<T> action)
        {
            monitoredItem.Notification += (sender, args) =>
            {
                var value = args.ToValue<T>();
                if (value is null)
                {
                    return;
                }
                action.Invoke(value);

            };
            return monitoredItem;
        }
        internal static MonitoredItem AppendDelegateToMonitoredItem<T>(this MonitoredItem monitoredItem, Func<T,Task> asyncFunc)
        {
            monitoredItem.Notification +=async  (sender, args) =>
            {
                if (sender.Handle is not null)
                {
                    sender.Handle = null;
                    return;
                }
                var value = args.ToValue<T>();
                if (value is null)
                {
                    return;
                }
                await asyncFunc.Invoke(value);

            };
            return monitoredItem;
        }

        internal static MonitoredItem AppendDelegateToMonitoredItem<T>(this MonitoredItem monitoredItem, Func<T, ValueTask> asyncFunc)
        {
            monitoredItem.Notification += async (sender, args) =>
            {
                var value = args.ToValue<T>();

                if (value is null)
                {
                    return;
                }
                await asyncFunc.Invoke(value);
            };
            return monitoredItem;
        }

        static MonitoredItem DefaultMonitoredItem(this NodeId nodeId)
        {
            return new MonitoredItem
            {
                AttributeId = Attributes.Value,
                StartNodeId = nodeId,
                DisplayName = nodeId.ToString(),
                MonitoringMode = MonitoringMode.Reporting,
                SamplingInterval = 10,
                DiscardOldest = true,
                CacheQueueSize = 0,
                NodeClass = NodeClass.Variable,
                QueueSize = 0,
                Filter = _dataChangeFilter
            };
        }
    }
}
