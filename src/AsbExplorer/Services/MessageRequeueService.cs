using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using AsbExplorer.Models;

namespace AsbExplorer.Services;

public class MessageRequeueService : IMessageRequeueService, IAsyncDisposable
{
    private readonly ConnectionStore _connectionStore;
    private ServiceBusClient? _client;
    private string? _currentConnectionName;

    public MessageRequeueService(ConnectionStore connectionStore)
    {
        _connectionStore = connectionStore;
    }

    public Task<RequeueResult> SendToQueueAsync(
        string connectionName,
        string queueName,
        PeekedMessage originalMessage,
        BinaryData? modifiedBody = null)
    {
        return SendToEntityAsync(connectionName, queueName, originalMessage, modifiedBody);
    }

    public Task<RequeueResult> SendToTopicAsync(
        string connectionName,
        string topicName,
        PeekedMessage originalMessage,
        BinaryData? modifiedBody = null)
    {
        return SendToEntityAsync(connectionName, topicName, originalMessage, modifiedBody);
    }

    public Task<RequeueResult> CompleteFromQueueDlqAsync(
        string connectionName,
        string queueName,
        long sequenceNumber)
    {
        return CompleteFromDlqAsync(connectionName, sequenceNumber, client => client.CreateReceiver(queueName, DlqReceiverOptions));
    }

    public Task<RequeueResult> CompleteFromSubscriptionDlqAsync(
        string connectionName,
        string topicName,
        string subscriptionName,
        long sequenceNumber)
    {
        return CompleteFromDlqAsync(connectionName, sequenceNumber, client => client.CreateReceiver(topicName, subscriptionName, DlqReceiverOptions));
    }

    private const int MaxConcurrency = 10;

    public async Task<BulkRequeueResult> RequeueMessagesAsync(
        string connectionName,
        string entityPath,
        string? topicName,
        IReadOnlyList<PeekedMessage> messages,
        bool removeOriginals,
        Action<int, int>? onProgress = null)
    {
        if (messages.Count == 0)
            return new BulkRequeueResult(0, 0, []);

        // Phase 1: Batch send all messages
        onProgress?.Invoke(0, messages.Count);
        var sendResult = await BatchSendMessagesAsync(connectionName, topicName ?? entityPath, messages);

        if (!sendResult.Success)
        {
            var failures = messages.Select(m => (m.SequenceNumber, sendResult.ErrorMessage ?? "Batch send failed")).ToList();
            onProgress?.Invoke(messages.Count, messages.Count);
            return new BulkRequeueResult(0, messages.Count, failures);
        }

        // All messages sent successfully
        if (!removeOriginals)
        {
            onProgress?.Invoke(messages.Count, messages.Count);
            return new BulkRequeueResult(messages.Count, 0, []);
        }

        // Phase 2: Batch complete originals from DLQ (single receiver, find all matches)
        var sequenceNumbers = messages.Select(m => m.SequenceNumber).ToHashSet();
        await BatchCompleteFromDlqAsync(connectionName, entityPath, topicName, sequenceNumbers, onProgress);

        // Complete failures are partial success - messages were sent, just not removed from DLQ
        onProgress?.Invoke(messages.Count, messages.Count);
        return new BulkRequeueResult(messages.Count, 0, []);
    }

    public async Task<BulkRequeueResult> DeleteMessagesAsync(
        string connectionName,
        string entityPath,
        string? topicName,
        IReadOnlyList<PeekedMessage> messages,
        Action<int, int>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0)
            return new BulkRequeueResult(0, 0, []);

        var sequenceNumbers = messages.Select(m => m.SequenceNumber).ToHashSet();
        var result = await BatchCompleteFromDlqAsync(connectionName, entityPath, topicName, sequenceNumbers, onProgress, cancellationToken);

        return result;
    }

    private async Task<BulkRequeueResult> BatchCompleteFromDlqAsync(
        string connectionName,
        string entityPath,
        string? topicName,
        HashSet<long> sequenceNumbers,
        Action<int, int>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var client = await GetOrCreateClientAsync(connectionName);
        if (client is null)
        {
            var connectionFailures = sequenceNumbers.Select(s => (s, "Connection not found")).ToList();
            return new BulkRequeueResult(0, sequenceNumbers.Count, connectionFailures);
        }

        await using var receiver = topicName is not null
            ? client.CreateReceiver(topicName, entityPath, DlqReceiverOptions)
            : client.CreateReceiver(entityPath, DlqReceiverOptions);

        var toComplete = new List<ServiceBusReceivedMessage>();
        var toAbandon = new List<ServiceBusReceivedMessage>();
        var foundSequenceNumbers = new HashSet<long>();

        // Receive all messages and categorize
        const int batchSize = 100;
        const int maxTotal = 1000;

        while (toComplete.Count + toAbandon.Count < maxTotal && foundSequenceNumbers.Count < sequenceNumbers.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(2), cancellationToken);
            if (batch.Count == 0)
                break;

            foreach (var msg in batch)
            {
                if (sequenceNumbers.Contains(msg.SequenceNumber))
                {
                    toComplete.Add(msg);
                    foundSequenceNumbers.Add(msg.SequenceNumber);
                }
                else
                {
                    toAbandon.Add(msg);
                }
            }
        }

        // Complete matching messages concurrently
        var completed = 0;
        var successCount = 0;
        var failures = new ConcurrentBag<(long SequenceNumber, string Error)>();

        await Parallel.ForEachAsync(
            toComplete,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency, CancellationToken = cancellationToken },
            async (msg, ct) =>
            {
                try
                {
                    await receiver.CompleteMessageAsync(msg, ct);
                    Interlocked.Increment(ref successCount);
                }
                catch (Exception ex)
                {
                    failures.Add((msg.SequenceNumber, ex.Message));
                }

                var current = Interlocked.Increment(ref completed);
                onProgress?.Invoke(current, sequenceNumbers.Count);
            });

        // Abandon non-matching messages concurrently (don't wait for all, fire and forget with brief delay)
        _ = Task.Run(async () =>
        {
            await Parallel.ForEachAsync(
                toAbandon,
                new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency },
                async (msg, _) =>
                {
                    try { await receiver.AbandonMessageAsync(msg); }
                    catch { /* Lock will expire eventually */ }
                });
        }, CancellationToken.None);

        // Report not-found messages as failures
        foreach (var seq in sequenceNumbers.Except(foundSequenceNumbers))
        {
            failures.Add((seq, "Message not found in DLQ"));
        }

        onProgress?.Invoke(sequenceNumbers.Count, sequenceNumbers.Count);
        return new BulkRequeueResult(successCount, failures.Count, failures.ToList());
    }

    private static ServiceBusReceiverOptions DlqReceiverOptions { get; } = new()
    {
        SubQueue = SubQueue.DeadLetter,
        ReceiveMode = ServiceBusReceiveMode.PeekLock
    };

    private async Task<RequeueResult> SendToEntityAsync(
        string connectionName,
        string entityName,
        PeekedMessage originalMessage,
        BinaryData? modifiedBody = null)
    {
        try
        {
            var client = await GetOrCreateClientAsync(connectionName);
            if (client is null)
            {
                return new RequeueResult(false, "Connection not found");
            }

            await using var sender = client.CreateSender(entityName);
            var message = CreateServiceBusMessage(originalMessage, modifiedBody);
            await sender.SendMessageAsync(message);

            return new RequeueResult(true, null);
        }
        catch (Exception ex)
        {
            return new RequeueResult(false, ex.Message);
        }
    }

    private async Task<RequeueResult> BatchSendMessagesAsync(
        string connectionName,
        string entityName,
        IReadOnlyList<PeekedMessage> messages)
    {
        try
        {
            var client = await GetOrCreateClientAsync(connectionName);
            if (client is null)
            {
                return new RequeueResult(false, "Connection not found");
            }

            await using var sender = client.CreateSender(entityName);

            // Use batching to handle size limits automatically
            ServiceBusMessageBatch? currentBatch = null;

            foreach (var originalMessage in messages)
            {
                var message = CreateServiceBusMessage(originalMessage, null);

                currentBatch ??= await sender.CreateMessageBatchAsync();

                if (!currentBatch.TryAddMessage(message))
                {
                    // Current batch is full, send it and start a new one
                    if (currentBatch.Count > 0)
                    {
                        await sender.SendMessagesAsync(currentBatch);
                    }

                    currentBatch = await sender.CreateMessageBatchAsync();

                    if (!currentBatch.TryAddMessage(message))
                    {
                        // Single message is too large for a batch
                        return new RequeueResult(false, $"Message {originalMessage.SequenceNumber} exceeds maximum batch size");
                    }
                }
            }

            // Send remaining messages
            if (currentBatch is { Count: > 0 })
            {
                await sender.SendMessagesAsync(currentBatch);
            }

            return new RequeueResult(true, null);
        }
        catch (Exception ex)
        {
            return new RequeueResult(false, ex.Message);
        }
    }

    private async Task<RequeueResult> CompleteFromDlqAsync(
        string connectionName,
        long sequenceNumber,
        Func<ServiceBusClient, ServiceBusReceiver> createReceiver)
    {
        try
        {
            var client = await GetOrCreateClientAsync(connectionName);
            if (client is null)
            {
                return new RequeueResult(false, "Connection not found");
            }

            await using var receiver = createReceiver(client);
            return await CompleteBySequenceNumberAsync(receiver, sequenceNumber);
        }
        catch (Exception ex)
        {
            return new RequeueResult(false, ex.Message);
        }
    }

    private static ServiceBusMessage CreateServiceBusMessage(PeekedMessage original, BinaryData? modifiedBody)
    {
        var message = new ServiceBusMessage(modifiedBody ?? original.Body)
        {
            MessageId = original.MessageId,
            ContentType = original.ContentType,
            Subject = original.Subject,
            CorrelationId = original.CorrelationId,
            SessionId = original.SessionId,
            TimeToLive = original.TimeToLive
        };

        foreach (var prop in original.ApplicationProperties)
        {
            message.ApplicationProperties[prop.Key] = prop.Value;
        }

        // Store original DLQ sequence number for audit trail
        message.ApplicationProperties["OriginalDlqSequenceNumber"] = original.SequenceNumber;

        return message;
    }

    private async Task<RequeueResult> CompleteBySequenceNumberAsync(
        ServiceBusReceiver receiver,
        long sequenceNumber)
    {
        // DLQ messages are not deferred, so we need to receive and find by sequence number.
        // Collect messages without abandoning until we find the target to avoid
        // re-receiving the same messages (abandoned messages go back to front of queue).
        var receivedMessages = new List<ServiceBusReceivedMessage>();
        ServiceBusReceivedMessage? targetMessage = null;

        try
        {
            const int maxMessages = 500;
            const int batchSize = 50;

            while (receivedMessages.Count < maxMessages)
            {
                var messages = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(2));

                if (messages.Count == 0)
                {
                    break;
                }

                foreach (var msg in messages)
                {
                    if (msg.SequenceNumber == sequenceNumber)
                    {
                        targetMessage = msg;
                    }
                    else
                    {
                        receivedMessages.Add(msg);
                    }
                }

                if (targetMessage is not null)
                {
                    break;
                }
            }

            if (targetMessage is null)
            {
                return new RequeueResult(false, $"Message with sequence number {sequenceNumber} not found in DLQ");
            }

            await receiver.CompleteMessageAsync(targetMessage);
            return new RequeueResult(true, null);
        }
        finally
        {
            foreach (var msg in receivedMessages)
            {
                try
                {
                    await receiver.AbandonMessageAsync(msg);
                }
                catch
                {
                    // Ignore abandon failures - lock will expire eventually
                }
            }
        }
    }

    private async Task<ServiceBusClient?> GetOrCreateClientAsync(string connectionName)
    {
        if (_client is null || _currentConnectionName != connectionName)
        {
            if (_client is not null)
            {
                await _client.DisposeAsync();
            }

            var connection = _connectionStore.GetByName(connectionName);
            if (connection is null)
            {
                return null;
            }

            _client = new ServiceBusClient(connection.ConnectionString);
            _currentConnectionName = connectionName;
        }

        return _client;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }
    }
}
