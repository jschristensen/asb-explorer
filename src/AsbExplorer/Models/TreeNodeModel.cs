namespace AsbExplorer.Models;

public record TreeNodeModel(
    string Id,
    string DisplayName,
    TreeNodeType NodeType,
    string? ConnectionName = null,
    string? EntityPath = null,
    string? ParentEntityPath = null,
    long? MessageCount = null,
    long? DlqMessageCount = null,
    bool IsLoadingCount = false
)
{
    public bool CanHaveChildren => NodeType is
        TreeNodeType.FavoritesRoot or
        TreeNodeType.ConnectionsRoot or
        TreeNodeType.Namespace or
        TreeNodeType.Queue or
        TreeNodeType.Topic or
        TreeNodeType.TopicSubscription or
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

    private bool HasDlqChild => NodeType is
        TreeNodeType.Queue or
        TreeNodeType.TopicSubscription;

    public string EffectiveDisplayName
    {
        get
        {
            if (IsFolderNode) return DisplayName;

            // Nodes with DLQ children show both counts: (99, D: 10)
            if (HasDlqChild)
            {
                if (MessageCount == -1 || DlqMessageCount == -1)
                    return $"{DisplayName} (?)";
                if (MessageCount.HasValue && DlqMessageCount.HasValue)
                    return $"{DisplayName} ({MessageCount}, D: {DlqMessageCount})";
                return DisplayName;
            }

            // Other nodes show single count
            if (MessageCount == -1) return $"{DisplayName} (?)";
            if (MessageCount.HasValue) return $"{DisplayName} ({MessageCount})";
            return DisplayName;
        }
    }
}
