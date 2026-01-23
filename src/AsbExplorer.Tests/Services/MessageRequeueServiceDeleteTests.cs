using AsbExplorer.Models;
using AsbExplorer.Services;

namespace AsbExplorer.Tests.Services;

/// <summary>
/// Tests for IMessageRequeueService.DeleteMessagesAsync behavior.
/// Uses a testable fake implementation since MessageRequeueService has dependencies
/// (ServiceBusClient, ConnectionStore) that are difficult to mock without a mocking framework.
/// </summary>
public class MessageRequeueServiceDeleteTests
{
    private static PeekedMessage CreateMessage(long sequenceNumber)
    {
        return new PeekedMessage(
            MessageId: $"msg-{sequenceNumber}",
            SequenceNumber: sequenceNumber,
            EnqueuedTime: DateTimeOffset.UtcNow,
            Subject: "Test Subject",
            DeliveryCount: 1,
            ContentType: "application/json",
            CorrelationId: null,
            SessionId: null,
            TimeToLive: TimeSpan.FromHours(1),
            ScheduledEnqueueTime: null,
            ApplicationProperties: new Dictionary<string, object>(),
            Body: BinaryData.FromString("{}")
        );
    }

    [Fact]
    public async Task DeleteMessagesAsync_SingleQueueMessage_CallsCompleteFromQueueDlq()
    {
        var service = new FakeMessageRequeueService();
        var messages = new[] { CreateMessage(42) };

        await service.DeleteMessagesAsync(
            connectionName: "test-conn",
            entityPath: "test-queue",
            topicName: null,
            messages: messages);

        Assert.Single(service.QueueDlqCompletions);
        Assert.Empty(service.SubscriptionDlqCompletions);
        Assert.Equal("test-conn", service.QueueDlqCompletions[0].ConnectionName);
        Assert.Equal("test-queue", service.QueueDlqCompletions[0].QueueName);
        Assert.Equal(42, service.QueueDlqCompletions[0].SequenceNumber);
    }

    [Fact]
    public async Task DeleteMessagesAsync_SingleSubscriptionMessage_CallsCompleteFromSubscriptionDlq()
    {
        var service = new FakeMessageRequeueService();
        var messages = new[] { CreateMessage(99) };

        await service.DeleteMessagesAsync(
            connectionName: "test-conn",
            entityPath: "test-subscription",
            topicName: "test-topic",
            messages: messages);

        Assert.Empty(service.QueueDlqCompletions);
        Assert.Single(service.SubscriptionDlqCompletions);
        Assert.Equal("test-conn", service.SubscriptionDlqCompletions[0].ConnectionName);
        Assert.Equal("test-topic", service.SubscriptionDlqCompletions[0].TopicName);
        Assert.Equal("test-subscription", service.SubscriptionDlqCompletions[0].SubscriptionName);
        Assert.Equal(99, service.SubscriptionDlqCompletions[0].SequenceNumber);
    }

    [Fact]
    public async Task DeleteMessagesAsync_MultipleMessages_ReportsProgress()
    {
        var service = new FakeMessageRequeueService();
        var messages = new[]
        {
            CreateMessage(1),
            CreateMessage(2),
            CreateMessage(3)
        };

        var progressCalls = new List<(int Current, int Total)>();

        await service.DeleteMessagesAsync(
            connectionName: "test-conn",
            entityPath: "test-queue",
            topicName: null,
            messages: messages,
            onProgress: (current, total) => progressCalls.Add((current, total)));

        // Should report progress for each message plus final completion
        Assert.Equal(4, progressCalls.Count);
        Assert.Equal((0, 3), progressCalls[0]); // Before first message
        Assert.Equal((1, 3), progressCalls[1]); // Before second message
        Assert.Equal((2, 3), progressCalls[2]); // Before third message
        Assert.Equal((3, 3), progressCalls[3]); // Completion
    }

    [Fact]
    public async Task DeleteMessagesAsync_PartialFailure_ReturnsCorrectCounts()
    {
        var service = new FakeMessageRequeueService();
        // Configure message 2 to fail
        service.FailingSequenceNumbers.Add(2);

        var messages = new[]
        {
            CreateMessage(1),
            CreateMessage(2),
            CreateMessage(3)
        };

        var result = await service.DeleteMessagesAsync(
            connectionName: "test-conn",
            entityPath: "test-queue",
            topicName: null,
            messages: messages);

        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.Single(result.Failures);
        Assert.Equal(2, result.Failures[0].SequenceNumber);
        Assert.Contains("Simulated failure", result.Failures[0].Error);
    }

    [Fact]
    public async Task DeleteMessagesAsync_AllFailures_ReturnsZeroSuccess()
    {
        var service = new FakeMessageRequeueService();
        service.FailingSequenceNumbers.Add(1);
        service.FailingSequenceNumbers.Add(2);

        var messages = new[]
        {
            CreateMessage(1),
            CreateMessage(2)
        };

        var result = await service.DeleteMessagesAsync(
            connectionName: "test-conn",
            entityPath: "test-queue",
            topicName: null,
            messages: messages);

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(2, result.FailureCount);
        Assert.Equal(2, result.Failures.Count);
    }

    [Fact]
    public async Task DeleteMessagesAsync_AllSuccess_ReturnsZeroFailures()
    {
        var service = new FakeMessageRequeueService();
        var messages = new[]
        {
            CreateMessage(1),
            CreateMessage(2),
            CreateMessage(3)
        };

        var result = await service.DeleteMessagesAsync(
            connectionName: "test-conn",
            entityPath: "test-queue",
            topicName: null,
            messages: messages);

        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public async Task DeleteMessagesAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var service = new FakeMessageRequeueService();
        var messages = new[]
        {
            CreateMessage(1),
            CreateMessage(2)
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.DeleteMessagesAsync(
                connectionName: "test-conn",
                entityPath: "test-queue",
                topicName: null,
                messages: messages,
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task DeleteMessagesAsync_EmptyList_ReturnsZeroCounts()
    {
        var service = new FakeMessageRequeueService();
        var messages = Array.Empty<PeekedMessage>();

        var result = await service.DeleteMessagesAsync(
            connectionName: "test-conn",
            entityPath: "test-queue",
            topicName: null,
            messages: messages);

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Empty(result.Failures);
    }

    /// <summary>
    /// Fake implementation of IMessageRequeueService for testing DeleteMessagesAsync logic.
    /// Tracks which completion methods would be called and allows configuring failures.
    /// </summary>
    private class FakeMessageRequeueService : IMessageRequeueService
    {
        public List<(string ConnectionName, string QueueName, long SequenceNumber)> QueueDlqCompletions { get; } = [];
        public List<(string ConnectionName, string TopicName, string SubscriptionName, long SequenceNumber)> SubscriptionDlqCompletions { get; } = [];
        public HashSet<long> FailingSequenceNumbers { get; } = [];

        public Task<RequeueResult> SendToQueueAsync(string connectionName, string queueName, PeekedMessage originalMessage, BinaryData? modifiedBody = null)
            => Task.FromResult(new RequeueResult(true, null));

        public Task<RequeueResult> SendToTopicAsync(string connectionName, string topicName, PeekedMessage originalMessage, BinaryData? modifiedBody = null)
            => Task.FromResult(new RequeueResult(true, null));

        public Task<RequeueResult> CompleteFromQueueDlqAsync(string connectionName, string queueName, long sequenceNumber)
        {
            QueueDlqCompletions.Add((connectionName, queueName, sequenceNumber));
            return Task.FromResult(FailingSequenceNumbers.Contains(sequenceNumber)
                ? new RequeueResult(false, $"Simulated failure for {sequenceNumber}")
                : new RequeueResult(true, null));
        }

        public Task<RequeueResult> CompleteFromSubscriptionDlqAsync(string connectionName, string topicName, string subscriptionName, long sequenceNumber)
        {
            SubscriptionDlqCompletions.Add((connectionName, topicName, subscriptionName, sequenceNumber));
            return Task.FromResult(FailingSequenceNumbers.Contains(sequenceNumber)
                ? new RequeueResult(false, $"Simulated failure for {sequenceNumber}")
                : new RequeueResult(true, null));
        }

        public Task<BulkRequeueResult> RequeueMessagesAsync(string connectionName, string entityPath, string? topicName, IReadOnlyList<PeekedMessage> messages, bool removeOriginals, Action<int, int>? onProgress = null)
            => Task.FromResult(new BulkRequeueResult(0, 0, []));

        public async Task<BulkRequeueResult> DeleteMessagesAsync(
            string connectionName,
            string entityPath,
            string? topicName,
            IReadOnlyList<PeekedMessage> messages,
            Action<int, int>? onProgress = null,
            CancellationToken cancellationToken = default)
        {
            var successCount = 0;
            var failures = new List<(long SequenceNumber, string Error)>();

            for (var i = 0; i < messages.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                onProgress?.Invoke(i, messages.Count);

                var message = messages[i];
                RequeueResult result;

                if (topicName != null)
                {
                    result = await CompleteFromSubscriptionDlqAsync(
                        connectionName, topicName, entityPath, message.SequenceNumber);
                }
                else
                {
                    result = await CompleteFromQueueDlqAsync(
                        connectionName, entityPath, message.SequenceNumber);
                }

                if (result.Success)
                    successCount++;
                else
                    failures.Add((message.SequenceNumber, result.ErrorMessage ?? "Unknown error"));
            }

            onProgress?.Invoke(messages.Count, messages.Count);
            return new BulkRequeueResult(successCount, failures.Count, failures);
        }
    }
}
