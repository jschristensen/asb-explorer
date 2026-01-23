namespace AsbExplorer.Models;

public static class CoreColumnRegistry
{
    private static readonly CoreColumnDefinition[] _columns =
    [
        new("SequenceNumber",    "#",             "sequence_number",        "INTEGER", true,  3,  12),
        new("MessageId",         "MessageId",     "message_id",             "TEXT",    true,  12, 14),
        new("Enqueued",          "Enqueued",      "enqueued_time",          "TEXT",    true,  10, 12),
        new("Subject",           "Subject",       "subject",                "TEXT",    true,  10, 30),
        new("Size",              "Size",          "body_size_bytes",        "INTEGER", true,  6,  8),
        new("DeliveryCount",     "Delivery",      "delivery_count",         "INTEGER", true,  3,  8),
        new("ContentType",       "ContentType",   "content_type",           "TEXT",    true,  8,  20),
        new("CorrelationId",     "CorrelationId", "correlation_id",         "TEXT",    false, 10, 14),
        new("SessionId",         "SessionId",     "session_id",             "TEXT",    false, 8,  14),
        new("TimeToLive",        "TimeToLive",    "time_to_live_seconds",   "REAL",    false, 6,  10),
        new("ScheduledEnqueue",  "Scheduled",     "scheduled_enqueue_time", "TEXT",    false, 8,  12)
    ];

    private static readonly Dictionary<string, CoreColumnDefinition> _byName =
        _columns.ToDictionary(c => c.Name);

    public static IReadOnlyList<CoreColumnDefinition> All => _columns;

    public static bool IsCore(string name) => _byName.ContainsKey(name);

    public static CoreColumnDefinition? Get(string name) =>
        _byName.TryGetValue(name, out var def) ? def : null;
}
