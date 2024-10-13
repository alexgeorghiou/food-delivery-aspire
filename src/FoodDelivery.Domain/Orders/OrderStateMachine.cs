namespace FoodDelivery.Domain.Orders;

/// <summary>
/// The single source of truth for which status changes are legal and who may make them.
/// </summary>
public static class OrderStateMachine
{
    private sealed record Rule(OrderStatus From, OrderStatus To, OrderActor[] Actors);

    private static readonly Rule[] Rules =
    [
        new(OrderStatus.Placed, OrderStatus.Accepted, [OrderActor.Restaurant]),
        new(OrderStatus.Placed, OrderStatus.Rejected, [OrderActor.Restaurant, OrderActor.System]),
        new(OrderStatus.Placed, OrderStatus.Cancelled, [OrderActor.Customer, OrderActor.System]),
        new(OrderStatus.Accepted, OrderStatus.Preparing, [OrderActor.Restaurant]),
        new(OrderStatus.Accepted, OrderStatus.Cancelled, [OrderActor.Customer, OrderActor.Restaurant, OrderActor.System]),
        new(OrderStatus.Preparing, OrderStatus.ReadyForPickup, [OrderActor.Restaurant]),
        new(OrderStatus.Preparing, OrderStatus.Cancelled, [OrderActor.Restaurant, OrderActor.System]),
        new(OrderStatus.ReadyForPickup, OrderStatus.CourierAssigned, [OrderActor.System]),
        new(OrderStatus.CourierAssigned, OrderStatus.PickedUp, [OrderActor.Courier]),
        new(OrderStatus.PickedUp, OrderStatus.Delivered, [OrderActor.Courier]),
    ];

    public static bool IsTerminal(OrderStatus status) =>
        status is OrderStatus.Delivered or OrderStatus.Cancelled or OrderStatus.Rejected;

    public static IReadOnlyCollection<OrderStatus> NextStatuses(OrderStatus from) =>
        Rules.Where(r => r.From == from).Select(r => r.To).ToArray();

    public static IReadOnlyCollection<OrderStatus> NextStatuses(OrderStatus from, OrderActor actor) =>
        Rules.Where(r => r.From == from && r.Actors.Contains(actor)).Select(r => r.To).ToArray();

    public static bool CanTransition(OrderStatus from, OrderStatus to, OrderActor actor) =>
        Rules.Any(r => r.From == from && r.To == to && r.Actors.Contains(actor));

    public static void EnsureCanTransition(OrderStatus from, OrderStatus to, OrderActor actor)
    {
        if (CanTransition(from, to, actor))
        {
            return;
        }

        var exists = Rules.Any(r => r.From == from && r.To == to);
        throw new InvalidOrderTransitionException(from, to, actor, exists
            ? $"{actor} is not allowed to move an order from {from} to {to}."
            : $"An order cannot move from {from} to {to}.");
    }
}
