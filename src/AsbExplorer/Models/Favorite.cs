namespace AsbExplorer.Models;

public record Favorite(
    string NamespaceFqdn,
    string EntityPath,
    TreeNodeType EntityType,
    string? ParentEntityPath = null
)
{
    public string DisplayName => ParentEntityPath is null
        ? $"{NamespaceFqdn}/{EntityPath}"
        : $"{NamespaceFqdn}/{ParentEntityPath}/{EntityPath}";
}
