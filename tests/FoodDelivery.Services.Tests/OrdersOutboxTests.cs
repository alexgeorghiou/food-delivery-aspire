using FoodDelivery.Domain.Events;
using FoodDelivery.Domain.Geo;
using FoodDelivery.Domain.Orders;
using FoodDelivery.Domain.Pricing;
using FoodDelivery.Messaging;
using FoodDelivery.Orders.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FoodDelivery.Services.Tests;

public class OrdersOutboxTests
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Placing_an_order_writes_an_outbox_message_in_the_same_save()
    {
        await using var db = NewContext(out _);
        var order = NewOrder();

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var message = Assert.Single(await db.Outbox.ToListAsync());
        Assert.Equal(EventRouting.OrderPlaced, message.RoutingKey);
        Assert.Equal(order.Id, message.OrderId);
        Assert.Null(message.PublishedAt);
    }

    [Fact]
    public async Task Ready_for_pickup_event_round_trips_through_the_outbox()
    {
        await using var db = NewContext(out _);
        var order = NewOrder();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        order.TransitionTo(OrderStatus.Accepted, OrderActor.Restaurant, T0.AddMinutes(1));
        order.TransitionTo(OrderStatus.Preparing, OrderActor.Restaurant, T0.AddMinutes(2));
        order.TransitionTo(OrderStatus.ReadyForPickup, OrderActor.Restaurant, T0.AddMinutes(12));
        await db.SaveChangesAsync();

        var message = await db.Outbox.SingleAsync(m => m.RoutingKey == EventRouting.OrderReadyForPickup);
        var ready = Assert.IsType<OrderReadyForPickup>(
            EventSerializer.Deserialize(message.RoutingKey, System.Text.Encoding.UTF8.GetBytes(message.Payload)));

        Assert.Equal(order.Id, ready.OrderId);
        Assert.Equal(52.3745, ready.PickupLatitude);
        Assert.Equal(52.3600, ready.DropoffLatitude);
    }

    [Fact]
    public async Task Order_lines_and_history_are_persisted_through_backing_fields()
    {
        var options = Options();
        var order = NewOrder();

        await using (var write = new OrdersDbContext(options, TimeProvider.System))
        {
            write.Orders.Add(order);
            order.TransitionTo(OrderStatus.Accepted, OrderActor.Restaurant, T0.AddMinutes(1));
            await write.SaveChangesAsync();
        }

        await using var read = new OrdersDbContext(options, TimeProvider.System);
        var loaded = await read.Orders.Include(o => o.Lines).Include(o => o.History).SingleAsync(o => o.Id == order.Id);

        Assert.Equal(2, loaded.Lines.Count);
        Assert.Equal([OrderStatus.Placed, OrderStatus.Accepted], loaded.History.OrderBy(h => h.At).Select(h => h.Status));
        Assert.Equal(OrderStatus.Accepted, loaded.Status);
        Assert.Equal(order.Total, loaded.Total);
    }

    private static DbContextOptions<OrdersDbContext> Options() =>
        new DbContextOptionsBuilder<OrdersDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private static OrdersDbContext NewContext(out DbContextOptions<OrdersDbContext> options)
    {
        options = Options();
        return new OrdersDbContext(options, TimeProvider.System);
    }

    private static Order NewOrder() => Order.Place(
        new RestaurantSnapshot(Guid.NewGuid(), "Luigi's Forno", new GeoPoint(52.3745, 4.8840)),
        "Ada",
        "Keizersgracht 1",
        new GeoPoint(52.3600, 4.8900),
        [
            new CartLine(Guid.NewGuid(), "Margherita", 9.50m, 1),
            new CartLine(Guid.NewGuid(), "Tiramisu", 6.00m, 2),
        ],
        PricingPolicy.Default,
        T0);
}
