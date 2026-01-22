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
    bool IsLoadingCount = false,
    TreeNodeType? SourceEntityType = null  // Original entity type for favorites
)
{
    // Use Id-based equality so TreeView's IndexOf works correctly after count updates
    public virtual bool Equals(TreeNodeModel? other) => other is not null && Id == other.Id;
    public override int GetHashCode() => Id.GetHashCode();

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

    private bool HasDlqChild => NodeType is
        TreeNodeType.Queue or
        TreeNodeType.TopicSubscription;

    public string EffectiveDisplayName => TreeNodeDisplayFormatter.Format(this).DisplayText;

    // Expose helper for formatter without public surface change
    internal bool HasDlqChildInternal() => HasDlqChild;
}
