namespace FoodDelivery.Domain.Orders;

public sealed class InvalidOrderTransitionException : InvalidOperationException
{
    public InvalidOrderTransitionException(OrderStatus from, OrderStatus to, OrderActor actor, string message)
        : base(message)
    {
        From = from;
        To = to;
        Actor = actor;
    }

    public OrderStatus From { get; }

    public OrderStatus To { get; }

    public OrderActor Actor { get; }
}
