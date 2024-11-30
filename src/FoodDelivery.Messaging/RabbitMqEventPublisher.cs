using System.Diagnostics;
using FoodDelivery.Domain.Events;
using RabbitMQ.Client;

namespace FoodDelivery.Messaging;

/// <summary>
/// Publishes to the topic exchange on a single long-lived channel with publisher confirms.
/// Channels are not thread-safe, so publishes are serialised with a semaphore.
/// </summary>
public sealed class RabbitMqEventPublisher(IConnection connection) : IEventPublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync(IIntegrationEvent integrationEvent, Guid messageId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var routingKey = EventRouting.RoutingKeyFor(integrationEvent);

        using var activity = MessagingDiagnostics.ActivitySource.StartActivity($"{routingKey} publish", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", EventRouting.Exchange);
        activity?.SetTag("messaging.rabbitmq.destination.routing_key", routingKey);
        activity?.SetTag("messaging.message.id", messageId.ToString());

        var properties = new BasicProperties
        {
            MessageId = messageId.ToString(),
            Type = routingKey,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>(),
        };
        MessagingDiagnostics.InjectTraceContext(properties.Headers);

        await _gate.WaitAsync(ct);
        try
        {
            var channel = await GetChannelAsync(ct);
            await channel.BasicPublishAsync(
                EventRouting.Exchange,
                routingKey,
                mandatory: false,
                basicProperties: properties,
                body: EventSerializer.Serialize(integrationEvent),
                cancellationToken: ct);
        }
        catch
        {
            // Drop the channel so the next publish starts on a fresh one.
            await ResetChannelAsync();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        var options = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);
        _channel = await connection.CreateChannelAsync(options, ct);
        await RabbitMqTopology.DeclareExchangesAsync(_channel, ct);
        return _channel;
    }

    private async Task ResetChannelAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ResetChannelAsync();
        _gate.Dispose();
    }
}
