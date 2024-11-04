namespace FoodDelivery.Domain.Orders;

public sealed class OrderStatusChange
{
    private OrderStatusChange()
    {
    }

    public OrderStatusChange(OrderStatus status, OrderActor actor, DateTimeOffset at, string? note)
    {
        Id = Guid.NewGuid();
        Status = status;
        Actor = actor;
        At = at;
        Note = note;
    }

    public Guid Id { get; private set; }

    public OrderStatus Status { get; private set; }

    public OrderActor Actor { get; private set; }

    public DateTimeOffset At { get; private set; }

    public string? Note { get; private set; }
}
