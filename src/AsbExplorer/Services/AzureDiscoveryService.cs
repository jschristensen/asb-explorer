using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.ServiceBus;
using AsbExplorer.Models;

namespace AsbExplorer.Services;

public class AzureDiscoveryService
{
    private readonly TokenCredential _credential;
    private readonly ArmClient _armClient;

    public AzureDiscoveryService()
    {
        _credential = new DefaultAzureCredential();
        _armClient = new ArmClient(_credential);
    }

    public TokenCredential Credential => _credential;

    public async Task<string?> GetCurrentUserAsync()
    {
        try
        {
            var context = new TokenRequestContext(["https://management.azure.com/.default"]);
            var token = await _credential.GetTokenAsync(context, default);

            // Decode JWT to get upn/email (simplified - just check if we can get a token)
            return token.Token.Length > 0 ? "Authenticated" : null;
        }
        catch
        {
            return null;
        }
    }

    public async IAsyncEnumerable<TreeNodeModel> GetSubscriptionsAsync()
    {
        await foreach (var sub in _armClient.GetSubscriptions())
        {
            yield return new TreeNodeModel(
                Id: sub.Data.SubscriptionId,
                DisplayName: $"{sub.Data.DisplayName} ({sub.Data.SubscriptionId[..8]}...)",
                NodeType: TreeNodeType.Subscription,
                SubscriptionId: sub.Data.SubscriptionId
            );
        }
    }

    public async IAsyncEnumerable<TreeNodeModel> GetNamespacesAsync(string subscriptionId)
    {
        var sub = _armClient.GetSubscriptionResource(
            SubscriptionResource.CreateResourceIdentifier(subscriptionId));

        await foreach (var ns in sub.GetServiceBusNamespacesAsync())
        {
            var resourceGroup = ns.Id.ResourceGroupName;
            var fqdn = $"{ns.Data.Name}.servicebus.windows.net";

            yield return new TreeNodeModel(
                Id: ns.Id.ToString(),
                DisplayName: ns.Data.Name,
                NodeType: TreeNodeType.Namespace,
                SubscriptionId: subscriptionId,
                ResourceGroupName: resourceGroup,
                NamespaceName: ns.Data.Name,
                NamespaceFqdn: fqdn
            );
        }
    }

    public async IAsyncEnumerable<TreeNodeModel> GetQueuesAsync(
        string subscriptionId,
        string resourceGroup,
        string namespaceName,
        string namespaceFqdn)
    {
        var nsId = ServiceBusNamespaceResource.CreateResourceIdentifier(
            subscriptionId, resourceGroup, namespaceName);
        var ns = _armClient.GetServiceBusNamespaceResource(nsId);

        await foreach (var queue in ns.GetServiceBusQueues())
        {
            // Main queue
            yield return new TreeNodeModel(
                Id: queue.Id.ToString(),
                DisplayName: queue.Data.Name,
                NodeType: TreeNodeType.Queue,
                SubscriptionId: subscriptionId,
                ResourceGroupName: resourceGroup,
                NamespaceName: namespaceName,
                NamespaceFqdn: namespaceFqdn,
                EntityPath: queue.Data.Name
            );

            // Dead-letter queue
            yield return new TreeNodeModel(
                Id: $"{queue.Id}/$deadletterqueue",
                DisplayName: $"{queue.Data.Name} (DLQ)",
                NodeType: TreeNodeType.QueueDeadLetter,
                SubscriptionId: subscriptionId,
                ResourceGroupName: resourceGroup,
                NamespaceName: namespaceName,
                NamespaceFqdn: namespaceFqdn,
                EntityPath: $"{queue.Data.Name}/$deadletterqueue"
            );
        }
    }

    public async IAsyncEnumerable<TreeNodeModel> GetTopicsAsync(
        string subscriptionId,
        string resourceGroup,
        string namespaceName,
        string namespaceFqdn)
    {
        var nsId = ServiceBusNamespaceResource.CreateResourceIdentifier(
            subscriptionId, resourceGroup, namespaceName);
        var ns = _armClient.GetServiceBusNamespaceResource(nsId);

        await foreach (var topic in ns.GetServiceBusTopics())
        {
            yield return new TreeNodeModel(
                Id: topic.Id.ToString(),
                DisplayName: topic.Data.Name,
                NodeType: TreeNodeType.Topic,
                SubscriptionId: subscriptionId,
                ResourceGroupName: resourceGroup,
                NamespaceName: namespaceName,
                NamespaceFqdn: namespaceFqdn,
                EntityPath: topic.Data.Name
            );
        }
    }

    public async IAsyncEnumerable<TreeNodeModel> GetTopicSubscriptionsAsync(
        string subscriptionId,
        string resourceGroup,
        string namespaceName,
        string namespaceFqdn,
        string topicName)
    {
        var topicId = ServiceBusTopicResource.CreateResourceIdentifier(
            subscriptionId, resourceGroup, namespaceName, topicName);
        var topic = _armClient.GetServiceBusTopicResource(topicId);

        await foreach (var sub in topic.GetServiceBusSubscriptions())
        {
            // Main subscription
            yield return new TreeNodeModel(
                Id: sub.Id.ToString(),
                DisplayName: sub.Data.Name,
                NodeType: TreeNodeType.TopicSubscription,
                SubscriptionId: subscriptionId,
                ResourceGroupName: resourceGroup,
                NamespaceName: namespaceName,
                NamespaceFqdn: namespaceFqdn,
                EntityPath: sub.Data.Name,
                ParentEntityPath: topicName
            );

            // Dead-letter queue
            yield return new TreeNodeModel(
                Id: $"{sub.Id}/$deadletterqueue",
                DisplayName: $"{sub.Data.Name} (DLQ)",
                NodeType: TreeNodeType.TopicSubscriptionDeadLetter,
                SubscriptionId: subscriptionId,
                ResourceGroupName: resourceGroup,
                NamespaceName: namespaceName,
                NamespaceFqdn: namespaceFqdn,
                EntityPath: $"{sub.Data.Name}/$deadletterqueue",
                ParentEntityPath: topicName
            );
        }
    }
}
