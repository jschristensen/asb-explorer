namespace AsbExplorer.Models;

public record TreeNodeModel(
    string Id,
    string DisplayName,
    TreeNodeType NodeType,
    string? ConnectionName = null,
    string? EntityPath = null,
    string? ParentEntityPath = null,
    long? MessageCount = null,
    bool IsLoadingCount = false
)
{
    public bool CanHaveChildren => NodeType is
        TreeNodeType.FavoritesRoot or
        TreeNodeType.ConnectionsRoot or
        TreeNodeType.Namespace or
        TreeNodeType.Topic or
        TreeNodeType.QueuesFolder or
        TreeNodeType.TopicsFolder;

    public bool CanPeekMessages => NodeType is
        TreeNodeType.Queue or
        TreeNodeType.QueueDeadLetter or
        TreeNodeType.TopicSubscription or
        TreeNodeType.TopicSubscriptionDeadLetter or
        TreeNodeType.Favorite;

    private bool IsFolderNode => NodeType is
        TreeNodeType.QueuesFolder or
        TreeNodeType.TopicsFolder;

    public string EffectiveDisplayName
    {
        get
        {
            if (IsFolderNode) return DisplayName;
            if (MessageCount == -1) return $"{DisplayName} (?)";
            if (MessageCount.HasValue) return $"{DisplayName} ({MessageCount})";
            return DisplayName;
        }
    }
}
