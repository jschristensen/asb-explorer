namespace AsbExplorer.Models;

public record TreeNodeModel(
    string Id,
    string DisplayName,
    TreeNodeType NodeType,
    string? SubscriptionId = null,
    string? ResourceGroupName = null,
    string? NamespaceName = null,
    string? NamespaceFqdn = null,
    string? EntityPath = null,
    string? ParentEntityPath = null
)
{
    public bool CanHaveChildren => NodeType is
        TreeNodeType.FavoritesRoot or
        TreeNodeType.SubscriptionsRoot or
        TreeNodeType.Subscription or
        TreeNodeType.ResourceGroup or
        TreeNodeType.Namespace or
        TreeNodeType.Topic;

    public bool CanPeekMessages => NodeType is
        TreeNodeType.Queue or
        TreeNodeType.QueueDeadLetter or
        TreeNodeType.TopicSubscription or
        TreeNodeType.TopicSubscriptionDeadLetter or
        TreeNodeType.Favorite;
}
