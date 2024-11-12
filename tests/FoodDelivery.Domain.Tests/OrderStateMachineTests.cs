using FoodDelivery.Domain.Events;
using FoodDelivery.Domain.Geo;
using FoodDelivery.Domain.Orders;
using FoodDelivery.Domain.Pricing;

namespace FoodDelivery.Domain.Tests;

public class OrderStateMachineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 2, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(OrderStatus.Placed, OrderStatus.Accepted, OrderActor.Restaurant)]
    [InlineData(OrderStatus.Placed, OrderStatus.Rejected, OrderActor.Restaurant)]
    [InlineData(OrderStatus.Placed, OrderStatus.Cancelled, OrderActor.Customer)]
    [InlineData(OrderStatus.Accepted, OrderStatus.Preparing, OrderActor.Restaurant)]
    [InlineData(OrderStatus.Preparing, OrderStatus.ReadyForPickup, OrderActor.Restaurant)]
    [InlineData(OrderStatus.ReadyForPickup, OrderStatus.CourierAssigned, OrderActor.System)]
    [InlineData(OrderStatus.CourierAssigned, OrderStatus.PickedUp, OrderActor.Courier)]
    [InlineData(OrderStatus.PickedUp, OrderStatus.Delivered, OrderActor.Courier)]
    public void Allowed_transitions(OrderStatus from, OrderStatus to, OrderActor actor)
    {
        Assert.True(OrderStateMachine.CanTransition(from, to, actor));
    }

    [Theory]
    [InlineData(OrderStatus.Placed, OrderStatus.Delivered, OrderActor.Courier)]
    [InlineData(OrderStatus.Placed, OrderStatus.Accepted, OrderActor.Customer)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Cancelled, OrderActor.Customer)]
    [InlineData(OrderStatus.ReadyForPickup, OrderStatus.PickedUp, OrderActor.Courier)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled, OrderActor.System)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Placed, OrderActor.Customer)]
    [InlineData(OrderStatus.PickedUp, OrderStatus.Delivered, OrderActor.Restaurant)]
    public void Disallowed_transitions(OrderStatus from, OrderStatus to, OrderActor actor)
    {
        Assert.False(OrderStateMachine.CanTransition(from, to, actor));
    }

    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Rejected)]
    public void Terminal_statuses_have_no_successors(OrderStatus status)
    {
        Assert.True(OrderStateMachine.IsTerminal(status));
        Assert.Empty(OrderStateMachine.NextStatuses(status));
    }

    [Fact]
    public void Every_non_terminal_status_has_a_way_forward()
    {
        foreach (var status in Enum.GetValues<OrderStatus>().Where(s => !OrderStateMachine.IsTerminal(s)))
        {
            Assert.NotEmpty(OrderStateMachine.NextStatuses(status));
        }
    }

    [Fact]
    public void Next_statuses_are_filtered_by_actor()
    {
        var customerOptions = OrderStateMachine.NextStatuses(OrderStatus.Placed, OrderActor.Customer);
        Assert.Equal([OrderStatus.Cancelled], customerOptions);
    }

    [Fact]
    public void Error_message_distinguishes_wrong_actor_from_impossible_move()
    {
        var wrongActor = Assert.Throws<InvalidOrderTransitionException>(
            () => OrderStateMachine.EnsureCanTransition(OrderStatus.Placed, OrderStatus.Accepted, OrderActor.Customer));
        var impossible = Assert.Throws<InvalidOrderTransitionException>(
            () => OrderStateMachine.EnsureCanTransition(OrderStatus.Placed, OrderStatus.Delivered, OrderActor.Courier));

        Assert.Contains("not allowed", wrongActor.Message, StringComparison.Ordinal);
        Assert.Contains("cannot move", impossible.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Happy_path_records_history_and_raises_events()
    {
        var order = NewOrder();
        Assert.IsType<OrderPlaced>(Assert.Single(order.DequeueEvents()));

        order.TransitionTo(OrderStatus.Accepted, OrderActor.Restaurant, T0.AddMinutes(1));
        order.TransitionTo(OrderStatus.Preparing, OrderActor.Restaurant, T0.AddMinutes(2));
        order.TransitionTo(OrderStatus.ReadyForPickup, OrderActor.Restaurant, T0.AddMinutes(15));

        var ready = Assert.IsType<OrderReadyForPickup>(Assert.Single(order.DequeueEvents()));
        Assert.Equal(order.Id, ready.OrderId);
        Assert.Equal(order.PickupLatitude, ready.PickupLatitude);

        var courier = Guid.NewGuid();
        order.AssignCourier(courier, "Sam", T0.AddMinutes(16));
        order.TransitionTo(OrderStatus.PickedUp, OrderActor.Courier, T0.AddMinutes(22));
        order.TransitionTo(OrderStatus.Delivered, OrderActor.Courier, T0.AddMinutes(35));

        var delivered = Assert.IsType<OrderDelivered>(Assert.Single(order.DequeueEvents()));
        Assert.Equal(courier, delivered.CourierId);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(7, order.History.Count);
        Assert.Equal(6, order.Version);
        Assert.Equal(T0.AddMinutes(35), order.UpdatedAt);
    }

    [Fact]
    public void Invalid_transition_leaves_the_order_untouched()
    {
        var order = NewOrder();

        Assert.Throws<InvalidOrderTransitionException>(
            () => order.TransitionTo(OrderStatus.PickedUp, OrderActor.Courier, T0.AddMinutes(1)));

        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.Single(order.History);
        Assert.Equal(0, order.Version);
    }

    [Fact]
    public void Courier_cannot_be_assigned_before_food_is_ready()
    {
        var order = NewOrder();
        order.TransitionTo(OrderStatus.Accepted, OrderActor.Restaurant, T0);

        Assert.Throws<InvalidOrderTransitionException>(() => order.AssignCourier(Guid.NewGuid(), "Sam", T0));
        Assert.Null(order.CourierId);
    }

    [Fact]
    public void Cancellation_raises_cancelled_event_with_reason()
    {
        var order = NewOrder();
        order.DequeueEvents();

        order.TransitionTo(OrderStatus.Cancelled, OrderActor.Customer, T0.AddMinutes(1), "Changed my mind");

        var cancelled = Assert.IsType<OrderCancelled>(Assert.Single(order.DequeueEvents()));
        Assert.Equal("Cancelled", cancelled.Reason);
        Assert.Equal("Changed my mind", cancelled.Note);
    }

    [Fact]
    public void Placing_an_order_snapshots_totals()
    {
        var order = NewOrder();

        Assert.Equal(19.00m, order.Subtotal);
        Assert.Equal(19.00m + 2.49m + 0.95m, order.Total);
        Assert.Equal("Luigi's", order.RestaurantName);
    }

    [Fact]
    public void Placing_an_empty_order_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Order.Place(
            new RestaurantSnapshot(Guid.NewGuid(), "Luigi's", new GeoPoint(51.5, -0.12)),
            "Ada", "1 Main St", new GeoPoint(51.51, -0.11), [], PricingPolicy.Default, T0));
    }

    [Fact]
    public void Routing_keys_are_mapped_for_every_event()
    {
        Assert.Equal(EventRouting.OrderReadyForPickup,
            EventRouting.RoutingKeyFor(new OrderReadyForPickup(Guid.NewGuid(), Guid.NewGuid(), "x", 0, 0, 0, 0, T0)));
        Assert.Equal(EventRouting.CourierAssigned,
            EventRouting.RoutingKeyFor(new CourierAssigned(Guid.NewGuid(), Guid.NewGuid(), "x", 1, T0)));
    }

    private static Order NewOrder() => Order.Place(
        new RestaurantSnapshot(Guid.NewGuid(), "Luigi's", new GeoPoint(51.5145, -0.1300)),
        "Ada",
        "10 Example Street",
        new GeoPoint(51.5200, -0.1000),
        [new CartLine(Guid.NewGuid(), "Margherita", 9.50m, 2)],
        PricingPolicy.Default,
        T0);
}
