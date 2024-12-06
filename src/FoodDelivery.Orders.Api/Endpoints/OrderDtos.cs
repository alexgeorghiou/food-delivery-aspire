using FoodDelivery.Domain.Orders;

namespace FoodDelivery.Orders.Api.Endpoints;

public sealed record PlaceOrderRequest(
    Guid RestaurantId,
    string CustomerName,
    string DeliveryAddress,
    double DeliveryLatitude,
    double DeliveryLongitude,
    IReadOnlyList<PlaceOrderLine> Lines);

public sealed record PlaceOrderLine(Guid MenuItemId, int Quantity);

public sealed record ChangeStatusRequest(OrderStatus Status, OrderActor Actor, string? Note);

public sealed record OrderDto(
    Guid Id,
    Guid RestaurantId,
    string RestaurantName,
    string CustomerName,
    string DeliveryAddress,
    OrderStatus Status,
    IReadOnlyList<OrderLineDto> Lines,
    OrderTotalsDto Totals,
    CourierDto? Courier,
    IReadOnlyList<OrderStatusChangeDto> History,
    IReadOnlyList<OrderStatus> NextStatuses,
    DateTimeOffset PlacedAt,
    DateTimeOffset UpdatedAt,
    int Version);

public sealed record OrderLineDto(Guid MenuItemId, string Name, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderTotalsDto(decimal Subtotal, decimal DeliveryFee, decimal SmallOrderSurcharge, decimal ServiceFee, decimal Total);

public sealed record CourierDto(Guid Id, string Name);

public sealed record OrderStatusChangeDto(OrderStatus Status, OrderActor Actor, DateTimeOffset At, string? Note);

public sealed record OrderSummaryDto(Guid Id, string RestaurantName, string CustomerName, OrderStatus Status, decimal Total, DateTimeOffset PlacedAt);

internal static class OrderMapping
{
    public static OrderDto ToDto(this Order order) => new(
        order.Id,
        order.RestaurantId,
        order.RestaurantName,
        order.CustomerName,
        order.DeliveryAddress,
        order.Status,
        order.Lines.Select(l => new OrderLineDto(l.MenuItemId, l.Name, l.UnitPrice, l.Quantity, l.LineTotal)).ToArray(),
        new OrderTotalsDto(order.Subtotal, order.DeliveryFee, order.SmallOrderSurcharge, order.ServiceFee, order.Total),
        order.CourierId is { } courierId ? new CourierDto(courierId, order.CourierName ?? "Courier") : null,
        order.History.OrderBy(h => h.At).Select(h => new OrderStatusChangeDto(h.Status, h.Actor, h.At, h.Note)).ToArray(),
        OrderStateMachine.NextStatuses(order.Status).ToArray(),
        order.PlacedAt,
        order.UpdatedAt,
        order.Version);
}
