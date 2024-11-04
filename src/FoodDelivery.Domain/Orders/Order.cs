using FoodDelivery.Domain.Events;
using FoodDelivery.Domain.Geo;
using FoodDelivery.Domain.Pricing;

namespace FoodDelivery.Domain.Orders;

/// <summary>
/// Order aggregate. All status changes go through <see cref="TransitionTo"/> so the
/// state machine rules and the audit history can never be bypassed.
/// </summary>
public sealed class Order
{
    private readonly List<OrderLine> _lines = [];
    private readonly List<OrderStatusChange> _history = [];
    private readonly List<IIntegrationEvent> _pendingEvents = [];

    private Order()
    {
        CustomerName = string.Empty;
        DeliveryAddress = string.Empty;
        RestaurantName = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid RestaurantId { get; private set; }

    public string RestaurantName { get; private set; }

    public double PickupLatitude { get; private set; }

    public double PickupLongitude { get; private set; }

    public string CustomerName { get; private set; }

    public string DeliveryAddress { get; private set; }

    public double DeliveryLatitude { get; private set; }

    public double DeliveryLongitude { get; private set; }

    public OrderStatus Status { get; private set; }

    public decimal Subtotal { get; private set; }

    public decimal DeliveryFee { get; private set; }

    public decimal SmallOrderSurcharge { get; private set; }

    public decimal ServiceFee { get; private set; }

    public decimal Total { get; private set; }

    public Guid? CourierId { get; private set; }

    public string? CourierName { get; private set; }

    public DateTimeOffset PlacedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Optimistic concurrency token, bumped on every status change.</summary>
    public int Version { get; private set; }

    public IReadOnlyList<OrderLine> Lines => _lines;

    public IReadOnlyList<OrderStatusChange> History => _history;

    public GeoPoint PickupLocation => new(PickupLatitude, PickupLongitude);

    public GeoPoint DeliveryLocation => new(DeliveryLatitude, DeliveryLongitude);

    public static Order Place(
        RestaurantSnapshot restaurant,
        string customerName,
        string deliveryAddress,
        GeoPoint deliveryLocation,
        IReadOnlyCollection<CartLine> lines,
        PricingPolicy pricing,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(restaurant);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(deliveryAddress);
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0)
        {
            throw new ArgumentException("An order needs at least one line.", nameof(lines));
        }

        var totals = Cart.CalculateTotals(lines, pricing);
        var order = new Order
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurant.Id,
            RestaurantName = restaurant.Name,
            PickupLatitude = restaurant.Location.Latitude,
            PickupLongitude = restaurant.Location.Longitude,
            CustomerName = customerName.Trim(),
            DeliveryAddress = deliveryAddress.Trim(),
            DeliveryLatitude = deliveryLocation.Latitude,
            DeliveryLongitude = deliveryLocation.Longitude,
            Status = OrderStatus.Placed,
            Subtotal = totals.Subtotal,
            DeliveryFee = totals.DeliveryFee,
            SmallOrderSurcharge = totals.SmallOrderSurcharge,
            ServiceFee = totals.ServiceFee,
            Total = totals.Total,
            PlacedAt = now,
            UpdatedAt = now,
        };

        order._lines.AddRange(lines.Select(l => new OrderLine(l.MenuItemId, l.Name, l.UnitPrice, l.Quantity)));
        order._history.Add(new OrderStatusChange(OrderStatus.Placed, OrderActor.Customer, now, null));
        order._pendingEvents.Add(new OrderPlaced(order.Id, order.RestaurantId, order.Total, now));
        return order;
    }

    public void TransitionTo(OrderStatus next, OrderActor actor, DateTimeOffset now, string? note = null)
    {
        OrderStateMachine.EnsureCanTransition(Status, next, actor);

        Status = next;
        UpdatedAt = now;
        Version++;
        _history.Add(new OrderStatusChange(next, actor, now, note));

        switch (next)
        {
            case OrderStatus.ReadyForPickup:
                _pendingEvents.Add(new OrderReadyForPickup(
                    Id, RestaurantId, RestaurantName,
                    PickupLatitude, PickupLongitude,
                    DeliveryLatitude, DeliveryLongitude, now));
                break;
            case OrderStatus.Delivered:
                _pendingEvents.Add(new OrderDelivered(Id, CourierId ?? Guid.Empty, now));
                break;
            case OrderStatus.Cancelled or OrderStatus.Rejected:
                _pendingEvents.Add(new OrderCancelled(Id, next.ToString(), note, now));
                break;
        }
    }

    public void AssignCourier(Guid courierId, string courierName, DateTimeOffset now)
    {
        if (courierId == Guid.Empty)
        {
            throw new ArgumentException("Courier id is required.", nameof(courierId));
        }

        TransitionTo(OrderStatus.CourierAssigned, OrderActor.System, now, $"Assigned to {courierName}");
        CourierId = courierId;
        CourierName = courierName;
    }

    /// <summary>Returns and clears integration events raised since the last call.</summary>
    public IReadOnlyList<IIntegrationEvent> DequeueEvents()
    {
        var events = _pendingEvents.ToArray();
        _pendingEvents.Clear();
        return events;
    }
}

public sealed record RestaurantSnapshot(Guid Id, string Name, GeoPoint Location);
