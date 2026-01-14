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

    public async Task<RequeueResult> SendToQueueAsync(
        string connectionName,
        string queueName,
        PeekedMessage originalMessage,
        BinaryData? modifiedBody = null)
    {
        try
        {
            var client = GetOrCreateClient(connectionName);
            if (client is null)
            {
                return new RequeueResult(false, "Connection not found");
            }

            await using var sender = client.CreateSender(queueName);
            var message = CreateServiceBusMessage(originalMessage, modifiedBody);
            await sender.SendMessageAsync(message);

            return new RequeueResult(true, null);
        }
        catch (Exception ex)
        {
            return new RequeueResult(false, ex.Message);
        }
    }

    public async Task<RequeueResult> SendToTopicAsync(
        string connectionName,
        string topicName,
        PeekedMessage originalMessage,
        BinaryData? modifiedBody = null)
    {
        try
        {
            var client = GetOrCreateClient(connectionName);
            if (client is null)
            {
                return new RequeueResult(false, "Connection not found");
            }

            await using var sender = client.CreateSender(topicName);
            var message = CreateServiceBusMessage(originalMessage, modifiedBody);
            await sender.SendMessageAsync(message);

            return new RequeueResult(true, null);
        }
        catch (Exception ex)
        {
            return new RequeueResult(false, ex.Message);
        }
    }

    public async Task<RequeueResult> CompleteFromQueueDlqAsync(
        string connectionName,
        string queueName,
        long sequenceNumber)
    {
        try
        {
            var client = GetOrCreateClient(connectionName);
            if (client is null)
            {
                return new RequeueResult(false, "Connection not found");
            }

            var options = new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            };

            await using var receiver = client.CreateReceiver(queueName, options);
            var message = await receiver.ReceiveDeferredMessageAsync(sequenceNumber);

            if (message is null)
            {
                // Message not deferred, try regular receive with filter
                return await CompleteBySequenceNumberAsync(receiver, sequenceNumber);
            }

            await receiver.CompleteMessageAsync(message);
            return new RequeueResult(true, null);
        }
        catch (Exception ex)
        {
            return new RequeueResult(false, ex.Message);
        }
    }

    public async Task<RequeueResult> CompleteFromSubscriptionDlqAsync(
        string connectionName,
        string topicName,
        string subscriptionName,
        long sequenceNumber)
    {
        try
        {
            var client = GetOrCreateClient(connectionName);
            if (client is null)
            {
                return new RequeueResult(false, "Connection not found");
            }

            var options = new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            };

            await using var receiver = client.CreateReceiver(topicName, subscriptionName, options);
            return await CompleteBySequenceNumberAsync(receiver, sequenceNumber);
        }
        catch (Exception ex)
        {
            return new RequeueResult(false, ex.Message);
        }
    }

    public async Task<BulkRequeueResult> RequeueMessagesAsync(
        string connectionName,
        string entityPath,
        string? topicName,
        IReadOnlyList<PeekedMessage> messages,
        bool removeOriginals)
    {
        var successCount = 0;
        var failures = new List<(long SequenceNumber, string Error)>();

        foreach (var message in messages)
        {
            // Send to original entity
            RequeueResult sendResult;
            if (topicName is not null)
            {
                sendResult = await SendToTopicAsync(connectionName, topicName, message);
            }
            else
            {
                sendResult = await SendToQueueAsync(connectionName, entityPath, message);
            }

            if (!sendResult.Success)
            {
                failures.Add((message.SequenceNumber, sendResult.ErrorMessage ?? "Unknown error"));
                continue;
            }

            // Complete original if requested
            if (removeOriginals)
            {
                RequeueResult completeResult;
                if (topicName is not null)
                {
                    completeResult = await CompleteFromSubscriptionDlqAsync(
                        connectionName, topicName, entityPath, message.SequenceNumber);
                }
                else
                {
                    completeResult = await CompleteFromQueueDlqAsync(
                        connectionName, entityPath, message.SequenceNumber);
                }

                if (!completeResult.Success)
                {
                    // Message was sent but not removed - partial success
                    // Still count as success since message was requeued
                    successCount++;
                    continue;
                }
            }

            successCount++;
        }

        return new BulkRequeueResult(successCount, failures.Count, failures);
    }

    private static ServiceBusMessage CreateServiceBusMessage(PeekedMessage original, BinaryData? modifiedBody)
    {
        var message = new ServiceBusMessage(modifiedBody ?? original.Body)
        {
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
            var maxMessages = 500;
            var batchSize = 50;

            while (receivedMessages.Count < maxMessages)
            {
                var messages = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(2));

                if (messages.Count == 0)
                {
                    // No more messages available
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

            // Complete the target message
            await receiver.CompleteMessageAsync(targetMessage);
            return new RequeueResult(true, null);
        }
        finally
        {
            // Abandon all non-target messages so they become available again
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

    private ServiceBusClient? GetOrCreateClient(string connectionName)
    {
        if (_client is null || _currentConnectionName != connectionName)
        {
            _client?.DisposeAsync().AsTask().GetAwaiter().GetResult();

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
