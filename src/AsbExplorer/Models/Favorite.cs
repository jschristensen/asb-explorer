namespace AsbExplorer.Models;

public record Favorite(
    string ConnectionName,
    string EntityPath,
    TreeNodeType EntityType,
    string? ParentEntityPath = null
)
{
    public string DisplayName => ParentEntityPath is null
        ? $"{ConnectionName}/{EntityPath}"
        : $"{ConnectionName}/{ParentEntityPath}/{EntityPath}";
}
