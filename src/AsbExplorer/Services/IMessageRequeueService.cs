using AsbExplorer.Models;

namespace AsbExplorer.Services;

public interface IMessageRequeueService
{
    /// <summary>
    /// Send a message to a queue.
    /// </summary>
    Task<RequeueResult> SendToQueueAsync(
        string connectionName,
        string queueName,
        PeekedMessage originalMessage,
        BinaryData? modifiedBody = null);

    /// <summary>
    /// Send a message to a topic subscription's originating topic.
    /// </summary>
    Task<RequeueResult> SendToTopicAsync(
        string connectionName,
        string topicName,
        PeekedMessage originalMessage,
        BinaryData? modifiedBody = null);

    /// <summary>
    /// Complete (remove) a message from a queue's dead-letter queue.
    /// </summary>
    Task<RequeueResult> CompleteFromQueueDlqAsync(
        string connectionName,
        string queueName,
        long sequenceNumber);

    /// <summary>
    /// Complete (remove) a message from a subscription's dead-letter queue.
    /// </summary>
    Task<RequeueResult> CompleteFromSubscriptionDlqAsync(
        string connectionName,
        string topicName,
        string subscriptionName,
        long sequenceNumber);

    /// <summary>
    /// Requeue multiple messages from DLQ to original entity.
    /// </summary>
    Task<BulkRequeueResult> RequeueMessagesAsync(
        string connectionName,
        string entityPath,
        string? topicName,
        IReadOnlyList<PeekedMessage> messages,
        bool removeOriginals,
        Action<int, int>? onProgress = null);

    /// <summary>
    /// Delete multiple messages from DLQ.
    /// </summary>
    Task<BulkRequeueResult> DeleteMessagesAsync(
        string connectionName,
        string entityPath,
        string? topicName,
        IReadOnlyList<PeekedMessage> messages,
        Action<int, int>? onProgress = null,
        CancellationToken cancellationToken = default);
}
