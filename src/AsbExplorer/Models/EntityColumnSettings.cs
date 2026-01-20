namespace AsbExplorer.Models;

public class EntityColumnSettings
{
    public List<ColumnConfig> Columns { get; set; } = [];
    public HashSet<string> DiscoveredProperties { get; set; } = [];
}
