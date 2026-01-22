using AsbExplorer.Helpers;
using AsbExplorer.Models;

namespace AsbExplorer.Tests.Helpers;

public class MessageFilterTests
{
    private static PeekedMessage CreateMessage(
        string messageId = "msg-123",
        string? subject = null,
        string? correlationId = null,
        string? sessionId = null,
        string? contentType = null,
        IReadOnlyDictionary<string, object>? applicationProperties = null,
        string? bodyText = null)
    {
        var body = BinaryData.FromString(bodyText ?? "");
        return new PeekedMessage(
            MessageId: messageId,
            SequenceNumber: 1,
            EnqueuedTime: DateTimeOffset.UtcNow,
            Subject: subject,
            DeliveryCount: 1,
            ContentType: contentType,
            CorrelationId: correlationId,
            SessionId: sessionId,
            TimeToLive: TimeSpan.FromHours(1),
            ScheduledEnqueueTime: null,
            ApplicationProperties: applicationProperties ?? new Dictionary<string, object>(),
            Body: body
        );
    }

    public class MatchesTests
    {
        [Fact]
        public void Matches_MessageId_ReturnsTrue()
        {
            var message = CreateMessage(messageId: "order-12345");
            Assert.True(MessageFilter.Matches(message, "order"));
        }

        [Fact]
        public void Matches_MessageId_CaseInsensitive()
        {
            var message = CreateMessage(messageId: "ORDER-12345");
            Assert.True(MessageFilter.Matches(message, "order"));
        }

        [Fact]
        public void Matches_NoMatch_ReturnsFalse()
        {
            var message = CreateMessage(messageId: "order-12345");
            Assert.False(MessageFilter.Matches(message, "invoice"));
        }

        [Fact]
        public void Matches_Subject_ReturnsTrue()
        {
            var message = CreateMessage(subject: "Payment received");
            Assert.True(MessageFilter.Matches(message, "payment"));
        }

        [Fact]
        public void Matches_CorrelationId_ReturnsTrue()
        {
            var message = CreateMessage(correlationId: "corr-abc-123");
            Assert.True(MessageFilter.Matches(message, "abc"));
        }

        [Fact]
        public void Matches_SessionId_ReturnsTrue()
        {
            var message = CreateMessage(sessionId: "session-xyz");
            Assert.True(MessageFilter.Matches(message, "xyz"));
        }

        [Fact]
        public void Matches_ContentType_ReturnsTrue()
        {
            var message = CreateMessage(contentType: "application/json");
            Assert.True(MessageFilter.Matches(message, "json"));
        }

        [Fact]
        public void Matches_NullSubject_DoesNotThrow()
        {
            var message = CreateMessage(subject: null);
            Assert.False(MessageFilter.Matches(message, "test"));
        }
    }
}
