using Azure.Messaging.ServiceBus.Administration;
using AsbExplorer.Models;

namespace AsbExplorer.Services;

public class ServiceBusConnectionService
{
    private readonly ConnectionStore _connectionStore;

    public ServiceBusConnectionService(ConnectionStore connectionStore)
    {
        _connectionStore = connectionStore;
    }

    public IEnumerable<TreeNodeModel> GetConnections()
    {
        return _connectionStore.Connections.Select(c => new TreeNodeModel(
            Id: $"conn:{c.Name}",
            DisplayName: c.Name,
            NodeType: TreeNodeType.Namespace,
            ConnectionName: c.Name
        ));
    }

    public async IAsyncEnumerable<TreeNodeModel> GetQueuesAsync(string connectionName)
    {
        var connection = _connectionStore.GetByName(connectionName);
        if (connection is null) yield break;

        var adminClient = new ServiceBusAdministrationClient(connection.ConnectionString);

        await foreach (var queue in adminClient.GetQueuesAsync())
        {
            // Main queue
            yield return new TreeNodeModel(
                Id: $"conn:{connectionName}:queue:{queue.Name}",
                DisplayName: queue.Name,
                NodeType: TreeNodeType.Queue,
                ConnectionName: connectionName,
                EntityPath: queue.Name
            );

            // Dead-letter queue
            yield return new TreeNodeModel(
                Id: $"conn:{connectionName}:queue:{queue.Name}:dlq",
                DisplayName: $"{queue.Name} (DLQ)",
                NodeType: TreeNodeType.QueueDeadLetter,
                ConnectionName: connectionName,
                EntityPath: queue.Name
            );
        }
    }

    public async IAsyncEnumerable<TreeNodeModel> GetTopicsAsync(string connectionName)
    {
        var connection = _connectionStore.GetByName(connectionName);
        if (connection is null) yield break;

        var adminClient = new ServiceBusAdministrationClient(connection.ConnectionString);

        await foreach (var topic in adminClient.GetTopicsAsync())
        {
            yield return new TreeNodeModel(
                Id: $"conn:{connectionName}:topic:{topic.Name}",
                DisplayName: topic.Name,
                NodeType: TreeNodeType.Topic,
                ConnectionName: connectionName,
                EntityPath: topic.Name
            );
        }
    }

    public async IAsyncEnumerable<TreeNodeModel> GetSubscriptionsAsync(string connectionName, string topicName)
    {
        var connection = _connectionStore.GetByName(connectionName);
        if (connection is null) yield break;

        var adminClient = new ServiceBusAdministrationClient(connection.ConnectionString);

        await foreach (var sub in adminClient.GetSubscriptionsAsync(topicName))
        {
            // Main subscription
            yield return new TreeNodeModel(
                Id: $"conn:{connectionName}:topic:{topicName}:sub:{sub.SubscriptionName}",
                DisplayName: sub.SubscriptionName,
                NodeType: TreeNodeType.TopicSubscription,
                ConnectionName: connectionName,
                EntityPath: sub.SubscriptionName,
                ParentEntityPath: topicName
            );

            // Dead-letter
            yield return new TreeNodeModel(
                Id: $"conn:{connectionName}:topic:{topicName}:sub:{sub.SubscriptionName}:dlq",
                DisplayName: $"{sub.SubscriptionName} (DLQ)",
                NodeType: TreeNodeType.TopicSubscriptionDeadLetter,
                ConnectionName: connectionName,
                EntityPath: sub.SubscriptionName,
                ParentEntityPath: topicName
            );
        }
    }
}
