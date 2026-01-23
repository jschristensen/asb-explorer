using AsbExplorer.Models;

namespace AsbExplorer.Services;

public class ColumnConfigService
{
    public List<ColumnConfig> GetDefaultColumns()
    {
        return CoreColumnRegistry.All
            .Select(c => new ColumnConfig(c.Name, c.DefaultVisible))
            .ToList();
    }

    public void MergeDiscoveredProperties(EntityColumnSettings settings, IEnumerable<string> newKeys)
    {
        foreach (var key in newKeys)
        {
            if (settings.DiscoveredProperties.Add(key))
            {
                // New property - add as hidden column at end
                settings.Columns.Add(new ColumnConfig(key, false, true));
            }
        }
    }

    public (bool IsValid, string? Error) ValidateConfig(List<ColumnConfig> columns)
    {
        if (!columns.Any(c => c.Visible))
        {
            return (false, "At least one column must be visible.");
        }

        var seqNumIndex = columns.FindIndex(c => c.Name == "SequenceNumber");
        if (seqNumIndex != 0)
        {
            return (false, "SequenceNumber must be the first column.");
        }

        if (!columns[0].Visible)
        {
            return (false, "SequenceNumber must be visible.");
        }

        return (true, null);
    }

    public List<ColumnConfig> GetVisibleColumns(List<ColumnConfig> columns)
    {
        return columns.Where(c => c.Visible).ToList();
    }
}
