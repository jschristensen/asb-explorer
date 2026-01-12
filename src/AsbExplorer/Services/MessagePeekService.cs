using Azure.Core;
using Azure.Messaging.ServiceBus;
using AsbExplorer.Models;

namespace AsbExplorer.Services;

public class MessagePeekService : IAsyncDisposable
{
    private readonly TokenCredential _credential;
    private ServiceBusClient? _client;
    private string? _currentNamespace;

    public MessagePeekService(TokenCredential credential)
    {
        _credential = credential;
    }

    public async Task<IReadOnlyList<PeekedMessage>> PeekMessagesAsync(
        string namespaceFqdn,
        string entityPath,
        string? topicName,
        bool isDeadLetter,
        int maxMessages = 50,
        long? fromSequenceNumber = null)
    {
        var client = GetOrCreateClient(namespaceFqdn);

        ServiceBusReceiver receiver;

        if (topicName is not null)
        {
            // Topic subscription
            var subName = isDeadLetter
                ? entityPath.Replace("/$deadletterqueue", "")
                : entityPath;

            var options = new ServiceBusReceiverOptions
            {
                SubQueue = isDeadLetter ? SubQueue.DeadLetter : SubQueue.None
            };

            receiver = client.CreateReceiver(topicName, subName, options);
        }
        else
        {
            // Queue
            var queueName = isDeadLetter
                ? entityPath.Replace("/$deadletterqueue", "")
                : entityPath;

            var options = new ServiceBusReceiverOptions
            {
                SubQueue = isDeadLetter ? SubQueue.DeadLetter : SubQueue.None
            };

            receiver = client.CreateReceiver(queueName, options);
        }

        await using (receiver)
        {
            var messages = fromSequenceNumber.HasValue
                ? await receiver.PeekMessagesAsync(maxMessages, fromSequenceNumber.Value)
                : await receiver.PeekMessagesAsync(maxMessages);

            return messages.Select(m => new PeekedMessage(
                MessageId: m.MessageId,
                SequenceNumber: m.SequenceNumber,
                EnqueuedTime: m.EnqueuedTime,
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

    private ServiceBusClient GetOrCreateClient(string namespaceFqdn)
    {
        if (_client is null || _currentNamespace != namespaceFqdn)
        {
            _client?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _client = new ServiceBusClient(namespaceFqdn, _credential);
            _currentNamespace = namespaceFqdn;
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
