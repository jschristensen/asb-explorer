using System.Text;
using System.Text.RegularExpressions;

namespace AsbExplorer.Helpers;

public static partial class ExportColumnHelper
{
    private static readonly Dictionary<string, string> CoreColumnMapping = new()
    {
        ["SequenceNumber"] = "sequence_number",
        ["MessageId"] = "message_id",
        ["Enqueued"] = "enqueued_time",
        ["Subject"] = "subject",
        ["Size"] = "body_size_bytes",
        ["DeliveryCount"] = "delivery_count",
        ["ContentType"] = "content_type",
        ["CorrelationId"] = "correlation_id",
        ["SessionId"] = "session_id",
        ["TimeToLive"] = "time_to_live_seconds",
        ["ScheduledEnqueue"] = "scheduled_enqueue_time"
    };

    public static string GetSqlColumnName(string displayName)
    {
        return CoreColumnMapping.TryGetValue(displayName, out var mapped)
            ? mapped
            : NormalizePropertyName(displayName);
    }

    public static string NormalizePropertyName(string propertyName)
    {
        // Replace non-alphanumeric with underscore, lowercase
        var normalized = InvalidCharsRegex().Replace(propertyName, "_").ToLowerInvariant();

        // Collapse multiple underscores
        normalized = MultipleUnderscoreRegex().Replace(normalized, "_");

        // Trim leading/trailing underscores
        normalized = normalized.Trim('_');

        // Add prefix and truncate (SQLite max identifier length is 128, but 63 is practical)
        var result = $"prop_{normalized}";
        if (result.Length > 63)
        {
            result = result[..63];
        }

        return result;
    }

    [GeneratedRegex(@"[^a-zA-Z0-9]+")]
    private static partial Regex InvalidCharsRegex();

    [GeneratedRegex(@"_+")]
    private static partial Regex MultipleUnderscoreRegex();
}
