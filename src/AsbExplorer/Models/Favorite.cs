namespace AsbExplorer.Models;

public record Favorite(
    string ConnectionName,
    string EntityPath,
    TreeNodeType EntityType,
    string? ParentEntityPath = null
)
{
    public bool IsDlq => EntityType is TreeNodeType.QueueDeadLetter or TreeNodeType.TopicSubscriptionDeadLetter;

    public string DisplayName
    {
        get
        {
            var baseName = ParentEntityPath is null
                ? $"{ConnectionName}/{EntityPath}"
                : $"{ConnectionName}/{ParentEntityPath}/{EntityPath}";

            return IsDlq ? $"{baseName}/DLQ" : baseName;
        }
    }
}
