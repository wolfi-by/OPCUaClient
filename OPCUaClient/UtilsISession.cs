
using Opc.Ua.Client;

namespace OPCUaClient
{
    internal static class UtilsISession
    {
        internal static Subscription Subscribe<T>(this ISession session, IOtNode node, Action<T> action)
        {
            var subscription = GetOrCreateSubscription(session, 1);
            var monitoredItem = session.
                Subscriptions
                .SelectMany(x => x.MonitoredItems)
                .FirstOrDefault(x => x.StartNodeId.ToString() == node.Endpoint);

            if (monitoredItem is not null)
            {
                return subscription;
            }

            var monItem = node.CreateMonitoredItem(action);
            subscription.AddItem(monItem);
            return subscription;
        }
        internal static Subscription Subscribe<T>(this ISession session, IOtNode node, Func<T, Task> asyncFunc)
        {
            var subscription = GetOrCreateSubscription(session, 1);
            var monitoredItem = session
                .Subscriptions
                .SelectMany(x => x.MonitoredItems)
                .FirstOrDefault(x => x.StartNodeId.ToString() == node.Endpoint);

            if (monitoredItem is not null)
            {
                return subscription;
            }
            var monItem = node.CreateMonitoredItem(asyncFunc);
            subscription.AddItem(monItem);
            return subscription;
        }
        internal static Subscription Subscribe<T>(this ISession session, IOtNode node, Func<T, ValueTask> asyncFunc)
        {
            var subscription = GetOrCreateSubscription(session, 1);
            var monitoredItem = session
                .Subscriptions
                .SelectMany(x => x.MonitoredItems)
                .FirstOrDefault(x => x.StartNodeId.ToString() == node.Endpoint);
            if (monitoredItem is not null)
            {
                return subscription;
            }
            var monItem = node.CreateMonitoredItem(asyncFunc);
            subscription.AddItem(monItem);
            return subscription;
        }

        internal static Subscription Subscribe<T>(this ISession session, IEnumerable<IOtNode> nodes, Action<T> action)
        {
            var subscription = GetOrCreateSubscription(session, nodes.Count());
            subscription.AddItems(nodes.CreateMonitoredItems(action));
            return subscription;
        }

        internal static Subscription Subscribe<T>(this ISession session, IEnumerable<IOtNode> nodes, Func<T, Task> action)
        {
            var subscription = GetOrCreateSubscription(session, nodes.Count());
            subscription.AddItems(nodes.CreateMonitoredItems(action));
            return subscription;
        }
        internal static Subscription Subscribe<T>(this ISession session, IEnumerable<IOtNode> nodes, Func<T, ValueTask> action)
        {
            var subscription = GetOrCreateSubscription(session, nodes.Count());
            subscription.AddItems(nodes.CreateMonitoredItems(action));
            return subscription;
        }
        internal static void Unsubscribe(this ISession session, IOtNode node)
        {
            var monitoredItems = session
                .Subscriptions
                .SelectMany(x => x.MonitoredItems)
                .Where(m => m.StartNodeId.ToString() == node.Endpoint);
            if (monitoredItems is null || !monitoredItems.Any())
            {
                return;
            }
            foreach (var monitoredItem in monitoredItems)
            {
                monitoredItem.DetachNotificationEventHandlers();
                monitoredItem.Subscription.RemoveItem(monitoredItem);
            }
        }
        static Subscription GetOrCreateSubscription(ISession session, int itemsToSubscripbe)
        {
            var subscription = session
                .Subscriptions
                .FirstOrDefault(x => x.MonitoredItemCount + itemsToSubscripbe <= OpcUaSessionConstants.MAX_ITEMS_PER_SUBSCRIPTION);
            return subscription ??= session.CreateDefaultAndAttach();
        }
        static Subscription CreateDefaultAndAttach(this ISession session)
        {
            var subscription = new Subscription(session.DefaultSubscription)
            {
                DisplayName = $"LaMeR.Swarm.{session.SessionName}",
                PublishingEnabled = true,
                PublishingInterval = 10,
                MaxMessageCount = 0,
                DisableMonitoredItemCache = false,
                MaxNotificationsPerPublish = OpcUaSessionConstants.MAX_ITEMS_PER_SUBSCRIPTION,
                Priority = 1,
            };
            session.AddSubscription(subscription);
            return subscription;

        }
    }
}
