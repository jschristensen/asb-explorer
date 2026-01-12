namespace AsbExplorer.Models;

public record TreeNodeModel(
    string Id,
    string DisplayName,
    TreeNodeType NodeType,
    string? ConnectionName = null,
    string? EntityPath = null,
    string? ParentEntityPath = null
)
{
    public bool CanHaveChildren => NodeType is
        TreeNodeType.FavoritesRoot or
        TreeNodeType.ConnectionsRoot or
        TreeNodeType.Namespace or
        TreeNodeType.Topic;

    public bool CanPeekMessages => NodeType is
        TreeNodeType.Queue or
        TreeNodeType.QueueDeadLetter or
        TreeNodeType.TopicSubscription or
        TreeNodeType.TopicSubscriptionDeadLetter or
        TreeNodeType.Favorite;
}
