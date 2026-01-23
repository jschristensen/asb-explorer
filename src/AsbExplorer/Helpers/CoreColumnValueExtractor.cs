using AsbExplorer.Models;

namespace AsbExplorer.Helpers;

public static class CoreColumnValueExtractor
{
    /// <summary>
    /// Gets a display-formatted value for UI rendering (truncated, formatted for readability).
    /// </summary>
    public static string GetDisplayValue(PeekedMessage msg, string columnName) => columnName switch
    {
        "SequenceNumber" => msg.SequenceNumber.ToString(),
        "MessageId" => DisplayHelpers.TruncateId(msg.MessageId, 12),
        "Enqueued" => DisplayHelpers.FormatRelativeTime(msg.EnqueuedTime),
        "Subject" => msg.Subject ?? "-",
        "Size" => DisplayHelpers.FormatSize(msg.BodySizeBytes),
        "DeliveryCount" => msg.DeliveryCount.ToString(),
        "ContentType" => msg.ContentType ?? "-",
        "CorrelationId" => msg.CorrelationId ?? "-",
        "SessionId" => msg.SessionId ?? "-",
        "TimeToLive" => DisplayHelpers.FormatTimeSpan(msg.TimeToLive),
        "ScheduledEnqueue" => DisplayHelpers.FormatScheduledTime(msg.ScheduledEnqueueTime),
        _ => "-"
    };

    /// <summary>
    /// Gets a raw value for export (full precision, ISO 8601 dates, numeric types).
    /// </summary>
    public static object? GetExportValue(PeekedMessage msg, string columnName) => columnName switch
    {
        "SequenceNumber" => msg.SequenceNumber,
        "MessageId" => msg.MessageId,
        "Enqueued" => msg.EnqueuedTime.ToString("o"),
        "Subject" => msg.Subject,
        "Size" => msg.BodySizeBytes,
        "DeliveryCount" => msg.DeliveryCount,
        "ContentType" => msg.ContentType,
        "CorrelationId" => msg.CorrelationId,
        "SessionId" => msg.SessionId,
        "TimeToLive" => msg.TimeToLive.TotalSeconds,
        "ScheduledEnqueue" => msg.ScheduledEnqueueTime?.ToString("o"),
        _ => null
    };
}
