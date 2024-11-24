using FoodDelivery.Domain.Events;
using RabbitMQ.Client;

namespace FoodDelivery.Messaging;

/// <summary>
/// Declares the exchanges and queues. Declarations are idempotent, so every service
/// declares what it needs on start-up instead of relying on deployment order.
/// </summary>
public static class RabbitMqTopology
{
    public const string DeadLetterExchange = "food-delivery.dlx";

    public static async Task DeclareExchangesAsync(IChannel channel, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        await channel.ExchangeDeclareAsync(EventRouting.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Fanout, durable: true, cancellationToken: ct);
    }

    /// <summary>
    /// Declares a durable work queue bound to the given routing keys, plus a matching
    /// "&lt;queue&gt;.dead" queue that receives messages rejected by the consumer.
    /// </summary>
    public static async Task DeclareQueueAsync(IChannel channel, string queue, IEnumerable<string> routingKeys, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(routingKeys);
        await DeclareExchangesAsync(channel, ct);

        var deadQueue = queue + ".dead";
        await channel.QueueDeclareAsync(deadQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(deadQueue, DeadLetterExchange, routingKey: queue, cancellationToken: ct);

        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = DeadLetterExchange,
            ["x-dead-letter-routing-key"] = queue,
        };

        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: arguments, cancellationToken: ct);
        foreach (var key in routingKeys)
        {
            await channel.QueueBindAsync(queue, EventRouting.Exchange, key, cancellationToken: ct);
        }
    }
}
