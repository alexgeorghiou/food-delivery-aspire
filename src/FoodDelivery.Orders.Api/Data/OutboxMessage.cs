namespace FoodDelivery.Orders.Api.Data;

/// <summary>
/// An integration event written in the same transaction as the order change that raised it.
/// The <see cref="Messaging.OutboxPublisher"/> relays it to RabbitMQ afterwards.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public required string RoutingKey { get; set; }

    public required string Payload { get; set; }

    public Guid OrderId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}

/// <summary>Records consumed message ids so redelivered messages are ignored.</summary>
public sealed class ProcessedMessage
{
    public required string MessageKey { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }
}
