namespace FoodDelivery.Domain.Events;

/// <summary>Marker for messages that cross service boundaries over the broker.</summary>
public interface IIntegrationEvent
{
    Guid OrderId { get; }
}

public static class EventRouting
{
    public const string Exchange = "food-delivery.events";

    public const string OrderPlaced = "order.placed";
    public const string OrderReadyForPickup = "order.ready";
    public const string OrderDelivered = "order.delivered";
    public const string OrderCancelled = "order.cancelled";
    public const string CourierAssigned = "courier.assigned";

    public static string RoutingKeyFor(IIntegrationEvent @event) => @event switch
    {
        Events.OrderPlaced => OrderPlaced,
        Events.OrderReadyForPickup => OrderReadyForPickup,
        Events.OrderDelivered => OrderDelivered,
        Events.OrderCancelled => OrderCancelled,
        Events.CourierAssigned => CourierAssigned,
        _ => throw new ArgumentOutOfRangeException(nameof(@event), @event.GetType().Name, "Unknown event type."),
    };
}

public sealed record OrderPlaced(Guid OrderId, Guid RestaurantId, decimal Total, DateTimeOffset PlacedAt) : IIntegrationEvent;

public sealed record OrderReadyForPickup(
    Guid OrderId,
    Guid RestaurantId,
    string RestaurantName,
    double PickupLatitude,
    double PickupLongitude,
    double DropoffLatitude,
    double DropoffLongitude,
    DateTimeOffset ReadyAt) : IIntegrationEvent;

public sealed record OrderDelivered(Guid OrderId, Guid CourierId, DateTimeOffset DeliveredAt) : IIntegrationEvent;

public sealed record OrderCancelled(Guid OrderId, string Reason, string? Note, DateTimeOffset CancelledAt) : IIntegrationEvent;

public sealed record CourierAssigned(
    Guid OrderId,
    Guid CourierId,
    string CourierName,
    double DistanceToPickupKm,
    DateTimeOffset AssignedAt) : IIntegrationEvent;
