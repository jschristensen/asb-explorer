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

    public async Task<BulkRequeueResult> RequeueMessagesAsync(
        string connectionName,
        string entityPath,
        string? topicName,
        IReadOnlyList<PeekedMessage> messages,
        bool removeOriginals,
        Action<int, int>? onProgress = null)
    {
        var successCount = 0;
        var failures = new List<(long SequenceNumber, string Error)>();

        for (var i = 0; i < messages.Count; i++)
        {
            onProgress?.Invoke(i, messages.Count);
            var message = messages[i];

            var targetEntity = topicName ?? entityPath;
            var sendResult = await SendToEntityAsync(connectionName, targetEntity, message);

            if (!sendResult.Success)
            {
                failures.Add((message.SequenceNumber, sendResult.ErrorMessage ?? "Unknown error"));
                continue;
            }

            if (removeOriginals)
            {
                var completeResult = topicName is not null
                    ? await CompleteFromSubscriptionDlqAsync(connectionName, topicName, entityPath, message.SequenceNumber)
                    : await CompleteFromQueueDlqAsync(connectionName, entityPath, message.SequenceNumber);

                // Message was sent but not removed - partial success
                // Still count as success since message was requeued
                if (!completeResult.Success)
                {
                    successCount++;
                    continue;
                }
            }

            successCount++;
        }

        onProgress?.Invoke(messages.Count, messages.Count);
        return new BulkRequeueResult(successCount, failures.Count, failures);
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
