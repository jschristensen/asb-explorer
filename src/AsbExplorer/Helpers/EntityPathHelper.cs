namespace AsbExplorer.Helpers;

public static class EntityPathHelper
{
    /// <summary>
    /// Returns the original entity path (queue name or subscription name).
    /// For DLQ messages, this is the entity to send requeued messages to.
    /// </summary>
    public static string? GetOriginalEntityPath(string? entityPath, bool isSubscription)
    {
        // Entity path is already the queue/subscription name.
        // The DLQ is accessed via SubQueue option, not path suffix.
        return entityPath;
    }
}
