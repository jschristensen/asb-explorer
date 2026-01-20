using AsbExplorer.Models;

namespace AsbExplorer.Services;

public class ColumnConfigService
{
    private static readonly List<(string Name, bool DefaultVisible)> CoreColumns =
    [
        ("SequenceNumber", true),
        ("MessageId", true),
        ("Enqueued", true),
        ("Subject", true),
        ("Size", true),
        ("DeliveryCount", true),
        ("ContentType", true),
        ("CorrelationId", false),
        ("SessionId", false),
        ("TimeToLive", false),
        ("ScheduledEnqueue", false)
    ];

    public List<ColumnConfig> GetDefaultColumns()
    {
        return CoreColumns
            .Select(c => new ColumnConfig(c.Name, c.DefaultVisible))
            .ToList();
    }
}
