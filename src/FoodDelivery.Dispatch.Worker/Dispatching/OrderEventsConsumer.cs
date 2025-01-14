using FoodDelivery.Domain.Events;
using FoodDelivery.Messaging;
using RabbitMQ.Client;

namespace FoodDelivery.Dispatch.Worker.Dispatching;

/// <summary>Consumes order lifecycle events that matter to dispatch.</summary>
public sealed class OrderEventsConsumer(
    IConnection connection,
    DispatchService dispatch,
    ILogger<OrderEventsConsumer> logger)
    : IntegrationEventConsumer(connection, logger)
{
    protected override string QueueName => "dispatch.order-events";

    protected override IReadOnlyCollection<string> RoutingKeys =>
        [EventRouting.OrderReadyForPickup, EventRouting.OrderDelivered, EventRouting.OrderCancelled];

    // Dispatch decisions are serialised anyway; a small prefetch keeps redelivery cheap.
    protected override ushort PrefetchCount => 4;

    protected override async Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken ct)
    {
        switch (integrationEvent)
        {
            case OrderReadyForPickup ready:
                await dispatch.DispatchAsync(ready, ct);
                break;
            case OrderDelivered or OrderCancelled:
                await dispatch.ReleaseAsync(integrationEvent.OrderId, ct);
                break;
        }
    }
}
