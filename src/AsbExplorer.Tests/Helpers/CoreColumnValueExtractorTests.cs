using AsbExplorer.Helpers;
using AsbExplorer.Models;

namespace AsbExplorer.Tests.Helpers;

public class CoreColumnValueExtractorTests
{
    private static PeekedMessage CreateTestMessage(
        long sequenceNumber = 42,
        string messageId = "msg-123-abc-456",
        DateTimeOffset? enqueuedTime = null,
        string? subject = "Test Subject",
        int deliveryCount = 3,
        string? contentType = "application/json",
        string? correlationId = "corr-123",
        string? sessionId = "session-456",
        TimeSpan? timeToLive = null,
        DateTimeOffset? scheduledEnqueueTime = null,
        IReadOnlyDictionary<string, object>? appProperties = null)
    {
        return new PeekedMessage(
            messageId,
            sequenceNumber,
            enqueuedTime ?? DateTimeOffset.UtcNow.AddMinutes(-30),
            subject,
            deliveryCount,
            contentType,
            correlationId,
            sessionId,
            timeToLive ?? TimeSpan.FromDays(1),
            scheduledEnqueueTime,
            appProperties ?? new Dictionary<string, object>(),
            BinaryData.FromString("test body")
        );
    }

    // GetDisplayValue tests
    [Fact]
    public void GetDisplayValue_SequenceNumber_ReturnsNumber()
    {
        var msg = CreateTestMessage(sequenceNumber: 12345);

        var result = CoreColumnValueExtractor.GetDisplayValue(msg, "SequenceNumber");

        Assert.Equal("12345", result);
    }

    [Fact]
    public void GetDisplayValue_MessageId_TruncatesLongIds()
    {
        var msg = CreateTestMessage(messageId: "very-long-message-id-12345678");

        var result = CoreColumnValueExtractor.GetDisplayValue(msg, "MessageId");

        Assert.Equal("very-long-me...", result);
    }

    [Fact]
    public void GetDisplayValue_Enqueued_ReturnsRelativeTime()
    {
        var msg = CreateTestMessage(enqueuedTime: DateTimeOffset.UtcNow.AddMinutes(-5));

        var result = CoreColumnValueExtractor.GetDisplayValue(msg, "Enqueued");

        Assert.Equal("5m ago", result);
    }

    [Fact]
    public void GetDisplayValue_Subject_ReturnsSubject()
    {
        var msg = CreateTestMessage(subject: "My Subject");

        var result = CoreColumnValueExtractor.GetDisplayValue(msg, "Subject");

        Assert.Equal("My Subject", result);
    }

    [Fact]
    public void GetDisplayValue_Subject_WhenNull_ReturnsDash()
    {
        var msg = CreateTestMessage(subject: null);

        var result = CoreColumnValueExtractor.GetDisplayValue(msg, "Subject");

        Assert.Equal("-", result);
    }

    [Fact]
    public void GetDisplayValue_Size_ReturnsFormattedSize()
    {
        var msg = new PeekedMessage(
            "msg-1", 1, DateTimeOffset.UtcNow, null, 1, null, null, null,
            TimeSpan.FromDays(1), null, new Dictionary<string, object>(),
            BinaryData.FromString(new string('x', 2048)) // ~2KB
        );

        var result = CoreColumnValueExtractor.GetDisplayValue(msg, "Size");

        Assert.Equal("2.0KB", result);
    }

    [Fact]
    public void GetDisplayValue_DeliveryCount_ReturnsCount()
    {
        var msg = CreateTestMessage(deliveryCount: 5);

        var result = CoreColumnValueExtractor.GetDisplayValue(msg, "DeliveryCount");

        Assert.Equal("5", result);
    }

    [Fact]
    public void GetDisplayValue_ContentType_ReturnsContentType()
    {
        var msg = CreateTestMessage(contentType: "text/plain");

        var result = CoreColumnValueExtractor.GetDisplayValue(msg, "ContentType");

        Assert.Equal("text/plain", result);
    }

    [Fact]
    public void GetDisplayValue_ContentType_WhenNull_ReturnsDash()
    {
        var msg = CreateTestMessage(contentType: null);

        var result = CoreColumnValueExtractor.GetDisplayValue(msg, "ContentType");

        Assert.Equal("-", result);
    }

    [Fact]
    public void GetDisplayValue_CorrelationId_ReturnsCorrelationId()
    {
        var msg = CreateTestMessage(correlationId: "corr-abc");

        var result = CoreColumnValueExtractor.GetDisplayValue(msg, "CorrelationId");

        Assert.Equal("corr-abc", result);
    }

    [Fact]
    public void GetDisplayValue_CorrelationId_WhenNull_ReturnsDash()
    {
        var msg = CreateTestMessage(correlationId: null);

        var result = CoreColumnValueExtractor.GetDisplayValue(msg, "CorrelationId");

        Assert.Equal("-", result);
    }

    [Fact]
    public void GetDisplayValue_SessionId_ReturnsSessionId()
    {
        var msg = CreateTestMessage(sessionId: "sess-xyz");

        var result = CoreColumnValueExtractor.GetDisplayValue(msg, "SessionId");

        Assert.Equal("sess-xyz", result);
    }

    [Fact]
    public void GetDisplayValue_SessionId_WhenNull_ReturnsDash()
    {
        var msg = CreateTestMessage(sessionId: null);

        var result = CoreColumnValueExtractor.GetDisplayValue(msg, "SessionId");

        Assert.Equal("-", result);
    }

    [Fact]
    public void GetDisplayValue_TimeToLive_ReturnsFormattedTimeSpan()
    {
        var msg = CreateTestMessage(timeToLive: TimeSpan.FromHours(2) + TimeSpan.FromMinutes(30));

        var result = CoreColumnValueExtractor.GetDisplayValue(msg, "TimeToLive");

        Assert.Equal("2h 30m", result);
    }

    [Fact]
    public void GetDisplayValue_ScheduledEnqueue_WhenNull_ReturnsDash()
    {
        var msg = CreateTestMessage(scheduledEnqueueTime: null);

        var result = CoreColumnValueExtractor.GetDisplayValue(msg, "ScheduledEnqueue");

        Assert.Equal("-", result);
    }

    [Fact]
    public void GetDisplayValue_UnknownColumn_ReturnsDash()
    {
        var msg = CreateTestMessage();

        var result = CoreColumnValueExtractor.GetDisplayValue(msg, "UnknownColumn");

        Assert.Equal("-", result);
    }

    // GetExportValue tests
    [Fact]
    public void GetExportValue_SequenceNumber_ReturnsLong()
    {
        var msg = CreateTestMessage(sequenceNumber: 12345);

        var result = CoreColumnValueExtractor.GetExportValue(msg, "SequenceNumber");

        Assert.Equal(12345L, result);
    }

    [Fact]
    public void GetExportValue_MessageId_ReturnsFullId()
    {
        var msg = CreateTestMessage(messageId: "full-message-id-123");

        var result = CoreColumnValueExtractor.GetExportValue(msg, "MessageId");

        Assert.Equal("full-message-id-123", result);
    }

    [Fact]
    public void GetExportValue_Enqueued_ReturnsIso8601()
    {
        var time = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var msg = CreateTestMessage(enqueuedTime: time);

        var result = CoreColumnValueExtractor.GetExportValue(msg, "Enqueued");

        Assert.Equal("2024-01-15T10:30:00.0000000+00:00", result);
    }

    [Fact]
    public void GetExportValue_Subject_ReturnsSubject()
    {
        var msg = CreateTestMessage(subject: "Test Subject");

        var result = CoreColumnValueExtractor.GetExportValue(msg, "Subject");

        Assert.Equal("Test Subject", result);
    }

    [Fact]
    public void GetExportValue_Subject_WhenNull_ReturnsNull()
    {
        var msg = CreateTestMessage(subject: null);

        var result = CoreColumnValueExtractor.GetExportValue(msg, "Subject");

        Assert.Null(result);
    }

    [Fact]
    public void GetExportValue_Size_ReturnsRawBytes()
    {
        var msg = new PeekedMessage(
            "msg-1", 1, DateTimeOffset.UtcNow, null, 1, null, null, null,
            TimeSpan.FromDays(1), null, new Dictionary<string, object>(),
            BinaryData.FromString("test body") // 9 bytes
        );

        var result = CoreColumnValueExtractor.GetExportValue(msg, "Size");

        Assert.Equal(9L, result);
    }

    [Fact]
    public void GetExportValue_DeliveryCount_ReturnsInt()
    {
        var msg = CreateTestMessage(deliveryCount: 7);

        var result = CoreColumnValueExtractor.GetExportValue(msg, "DeliveryCount");

        Assert.Equal(7, result);
    }

    [Fact]
    public void GetExportValue_TimeToLive_ReturnsTotalSeconds()
    {
        var msg = CreateTestMessage(timeToLive: TimeSpan.FromHours(2));

        var result = CoreColumnValueExtractor.GetExportValue(msg, "TimeToLive");

        Assert.Equal(7200.0, result);
    }

    [Fact]
    public void GetExportValue_ScheduledEnqueue_ReturnsIso8601()
    {
        var time = new DateTimeOffset(2024, 6, 20, 14, 0, 0, TimeSpan.Zero);
        var msg = CreateTestMessage(scheduledEnqueueTime: time);

        var result = CoreColumnValueExtractor.GetExportValue(msg, "ScheduledEnqueue");

        Assert.Equal("2024-06-20T14:00:00.0000000+00:00", result);
    }

    [Fact]
    public void GetExportValue_ScheduledEnqueue_WhenNull_ReturnsNull()
    {
        var msg = CreateTestMessage(scheduledEnqueueTime: null);

        var result = CoreColumnValueExtractor.GetExportValue(msg, "ScheduledEnqueue");

        Assert.Null(result);
    }

    [Fact]
    public void GetExportValue_UnknownColumn_ReturnsNull()
    {
        var msg = CreateTestMessage();

        var result = CoreColumnValueExtractor.GetExportValue(msg, "UnknownColumn");

        Assert.Null(result);
    }
}
