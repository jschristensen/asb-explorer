using Azure.Messaging.ServiceBus;
using AsbExplorer.Models;

namespace AsbExplorer.Services;

public class MessagePeekService : IAsyncDisposable
{
    private readonly ConnectionStore _connectionStore;
    private ServiceBusClient? _client;
    private string? _currentConnectionName;

    public MessagePeekService(ConnectionStore connectionStore)
    {
        _connectionStore = connectionStore;
    }

    public async Task<IReadOnlyList<PeekedMessage>> PeekMessagesAsync(
        string connectionName,
        string entityPath,
        string? topicName,
        bool isDeadLetter,
        int maxMessages = 50,
        long? fromSequenceNumber = null)
    {
        var client = GetOrCreateClient(connectionName);
        if (client is null)
        {
            return [];
        }

        ServiceBusReceiver receiver;

        if (topicName is not null)
        {
            // Topic subscription
            var options = new ServiceBusReceiverOptions
            {
                SubQueue = isDeadLetter ? SubQueue.DeadLetter : SubQueue.None
            };

            receiver = client.CreateReceiver(topicName, entityPath, options);
        }
        else
        {
            // Queue
            var options = new ServiceBusReceiverOptions
            {
                SubQueue = isDeadLetter ? SubQueue.DeadLetter : SubQueue.None
            };

            receiver = client.CreateReceiver(entityPath, options);
        }

        await using (receiver)
        {
            // Azure Service Bus SDK limits PeekMessagesAsync to 250 messages per call.
            // To retrieve more, we need to paginate using fromSequenceNumber.
            const int batchSize = 250;
            var allMessages = new List<ServiceBusReceivedMessage>();
            long? currentFromSequence = fromSequenceNumber;

            while (allMessages.Count < maxMessages)
            {
                var remaining = maxMessages - allMessages.Count;
                var toFetch = Math.Min(remaining, batchSize);

                var batch = currentFromSequence.HasValue
                    ? await receiver.PeekMessagesAsync(toFetch, currentFromSequence.Value)
                    : await receiver.PeekMessagesAsync(toFetch);

                if (batch.Count == 0)
                    break;

                allMessages.AddRange(batch);

                // Next batch starts after the last message's sequence number
                currentFromSequence = batch[^1].SequenceNumber + 1;
            }

            return allMessages.Select(m => new PeekedMessage(
                MessageId: m.MessageId,
                SequenceNumber: m.SequenceNumber,
                EnqueuedTime: m.EnqueuedTime,
                Subject: m.Subject,
                DeliveryCount: m.DeliveryCount,
                ContentType: m.ContentType,
                CorrelationId: m.CorrelationId,
                SessionId: m.SessionId,
                TimeToLive: m.TimeToLive,
                ScheduledEnqueueTime: m.ScheduledEnqueueTime == default
                    ? null
                    : m.ScheduledEnqueueTime,
                ApplicationProperties: m.ApplicationProperties,
                Body: m.Body
            )).ToList();
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
