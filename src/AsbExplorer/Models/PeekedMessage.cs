namespace AsbExplorer.Models;

public record PeekedMessage(
    string MessageId,
    long SequenceNumber,
    DateTimeOffset EnqueuedTime,
    int DeliveryCount,
    string? ContentType,
    string? CorrelationId,
    string? SessionId,
    TimeSpan TimeToLive,
    DateTimeOffset? ScheduledEnqueueTime,
    IReadOnlyDictionary<string, object> ApplicationProperties,
    BinaryData Body
)
{
    public long BodySizeBytes => Body.ToMemory().Length;
}
