using AsbExplorer.Models;
using AsbExplorer.Services;

namespace AsbExplorer.Tests.Services;

public class ApplicationPropertyScannerTests
{
    private readonly ApplicationPropertyScanner _scanner = new();

    private static PeekedMessage CreateMessage(Dictionary<string, object>? props = null)
    {
        return new PeekedMessage(
            MessageId: Guid.NewGuid().ToString(),
            SequenceNumber: 1,
            EnqueuedTime: DateTimeOffset.UtcNow,
            Subject: null,
            DeliveryCount: 1,
            ContentType: null,
            CorrelationId: null,
            SessionId: null,
            TimeToLive: TimeSpan.FromHours(1),
            ScheduledEnqueueTime: null,
            ApplicationProperties: props ?? new Dictionary<string, object>(),
            Body: BinaryData.FromString("{}")
        );
    }

    [Fact]
    public void ScanMessages_ReturnsDistinctKeys()
    {
        var messages = new List<PeekedMessage>
        {
            CreateMessage(new Dictionary<string, object> { ["OrderId"] = "123", ["CustomerId"] = "456" }),
            CreateMessage(new Dictionary<string, object> { ["OrderId"] = "789", ["Status"] = "pending" })
        };

        var keys = _scanner.ScanMessages(messages);

        Assert.Equal(3, keys.Count);
        Assert.Contains("OrderId", keys);
        Assert.Contains("CustomerId", keys);
        Assert.Contains("Status", keys);
    }

    [Fact]
    public void ScanMessages_RespectsLimit()
    {
        var messages = new List<PeekedMessage>
        {
            CreateMessage(new Dictionary<string, object> { ["Prop1"] = "1" }),
            CreateMessage(new Dictionary<string, object> { ["Prop2"] = "2" }),
            CreateMessage(new Dictionary<string, object> { ["Prop3"] = "3" })
        };

        var keys = _scanner.ScanMessages(messages, limit: 2);

        Assert.Equal(2, keys.Count);
        Assert.Contains("Prop1", keys);
        Assert.Contains("Prop2", keys);
        Assert.DoesNotContain("Prop3", keys);
    }

    [Fact]
    public void ScanMessages_HandlesEmptyMessages()
    {
        var keys = _scanner.ScanMessages([]);

        Assert.Empty(keys);
    }

    [Fact]
    public void ScanMessages_HandlesNullProperties()
    {
        var messages = new List<PeekedMessage>
        {
            CreateMessage(null),
            CreateMessage(new Dictionary<string, object> { ["Valid"] = "value" })
        };

        var keys = _scanner.ScanMessages(messages);

        Assert.Single(keys);
        Assert.Contains("Valid", keys);
    }
}
