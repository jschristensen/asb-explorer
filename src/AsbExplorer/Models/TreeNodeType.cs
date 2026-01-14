namespace AsbExplorer.Models;

public enum TreeNodeType
{
    FavoritesRoot,
    Favorite,
    ConnectionsRoot,
    Namespace,
    Queue,
    QueueDeadLetter,
    Topic,
    TopicSubscription,
    TopicSubscriptionDeadLetter,
    QueuesFolder,
    TopicsFolder,
    Placeholder  // For loading/error states - can't have children, can't peek
}
