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

        [Fact]
        public void Matches_ApplicationPropertyKey_ReturnsTrue()
        {
            var props = new Dictionary<string, object> { { "CustomerId", "12345" } };
            var message = CreateMessage(applicationProperties: props);
            Assert.True(MessageFilter.Matches(message, "customer"));
        }

        [Fact]
        public void Matches_ApplicationPropertyValue_ReturnsTrue()
        {
            var props = new Dictionary<string, object> { { "Status", "Completed" } };
            var message = CreateMessage(applicationProperties: props);
            Assert.True(MessageFilter.Matches(message, "completed"));
        }

        [Fact]
        public void Matches_ApplicationPropertyIntValue_ReturnsTrue()
        {
            var props = new Dictionary<string, object> { { "RetryCount", 42 } };
            var message = CreateMessage(applicationProperties: props);
            Assert.True(MessageFilter.Matches(message, "42"));
        }

        [Fact]
        public void Matches_BodyContent_ReturnsTrue()
        {
            var message = CreateMessage(bodyText: "{\"orderId\": \"ORD-999\"}");
            Assert.True(MessageFilter.Matches(message, "ORD-999"));
        }

        [Fact]
        public void Matches_BodyContent_CaseInsensitive()
        {
            var message = CreateMessage(bodyText: "{\"status\": \"COMPLETED\"}");
            Assert.True(MessageFilter.Matches(message, "completed"));
        }

        [Fact]
        public void Matches_BinaryBody_DoesNotThrow()
        {
            // Create message with invalid UTF-8 bytes
            var invalidUtf8 = new byte[] { 0xFF, 0xFE, 0x00, 0x01 };
            var message = new PeekedMessage(
                MessageId: "msg-1",
                SequenceNumber: 1,
                EnqueuedTime: DateTimeOffset.UtcNow,
                Subject: null,
                DeliveryCount: 1,
                ContentType: null,
                CorrelationId: null,
                SessionId: null,
                TimeToLive: TimeSpan.FromHours(1),
                ScheduledEnqueueTime: null,
                ApplicationProperties: new Dictionary<string, object>(),
                Body: BinaryData.FromBytes(invalidUtf8)
            );
            // Should not throw, and should not match (binary content)
            Assert.False(MessageFilter.Matches(message, "test"));
        }

        [Fact]
        public void Matches_BinaryBody_StillMatchesOtherFields()
        {
            var invalidUtf8 = new byte[] { 0xFF, 0xFE, 0x00, 0x01 };
            var message = new PeekedMessage(
                MessageId: "order-123",
                SequenceNumber: 1,
                EnqueuedTime: DateTimeOffset.UtcNow,
                Subject: null,
                DeliveryCount: 1,
                ContentType: null,
                CorrelationId: null,
                SessionId: null,
                TimeToLive: TimeSpan.FromHours(1),
                ScheduledEnqueueTime: null,
                ApplicationProperties: new Dictionary<string, object>(),
                Body: BinaryData.FromBytes(invalidUtf8)
            );
            // Should match on MessageId even though body is binary
            Assert.True(MessageFilter.Matches(message, "order"));
        }
    }

    public class ApplyTests
    {
        [Fact]
        public void Apply_EmptySearchTerm_ReturnsAllMessages()
        {
            var messages = new List<PeekedMessage>
            {
                CreateMessage(messageId: "msg-1"),
                CreateMessage(messageId: "msg-2"),
                CreateMessage(messageId: "msg-3")
            };

            var result = MessageFilter.Apply(messages, "");

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void Apply_WithSearchTerm_ReturnsOnlyMatches()
        {
            var messages = new List<PeekedMessage>
            {
                CreateMessage(messageId: "order-1"),
                CreateMessage(messageId: "invoice-2"),
                CreateMessage(messageId: "order-3")
            };

            var result = MessageFilter.Apply(messages, "order");

            Assert.Equal(2, result.Count);
            Assert.All(result, m => Assert.Contains("order", m.MessageId));
        }

        [Fact]
        public void Apply_NoMatches_ReturnsEmptyList()
        {
            var messages = new List<PeekedMessage>
            {
                CreateMessage(messageId: "msg-1"),
                CreateMessage(messageId: "msg-2")
            };

            var result = MessageFilter.Apply(messages, "xyz");

            Assert.Empty(result);
        }

        [Fact]
        public void Apply_PreservesOrder()
        {
            var messages = new List<PeekedMessage>
            {
                CreateMessage(messageId: "order-1"),
                CreateMessage(messageId: "order-2"),
                CreateMessage(messageId: "order-3")
            };

            var result = MessageFilter.Apply(messages, "order");

            Assert.Equal("order-1", result[0].MessageId);
            Assert.Equal("order-2", result[1].MessageId);
            Assert.Equal("order-3", result[2].MessageId);
        }
    }
}
