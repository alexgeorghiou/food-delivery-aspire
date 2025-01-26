using FoodDelivery.Dispatch.Worker.Dispatching;
using FoodDelivery.Domain.Dispatch;
using FoodDelivery.Domain.Events;
using FoodDelivery.Domain.Geo;
using Microsoft.Extensions.Logging.Abstractions;

namespace FoodDelivery.Services.Tests;

public sealed class DispatchServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 19, 0, 0, TimeSpan.Zero);
    private static readonly GeoPoint Pickup = new(52.3745, 4.8840);

    private readonly InMemoryCourierStore _store = new();
    private readonly RecordingPublisher _publisher = new();
    private readonly FixedClock _clock = new(Now);
    private readonly DispatchService _service;

    public DispatchServiceTests()
    {
        _service = new DispatchService(
            _store, _publisher, new CourierMatcher(), DispatchOptions.Default, _clock, NullLogger<DispatchService>.Instance);
    }

    [Fact]
    public async Task Assigns_nearest_courier_and_publishes_event()
    {
        var near = await AddCourierAsync("near", 0.003);
        await AddCourierAsync("far", 0.03);
        var order = ReadyOrder();

        var decision = await _service.DispatchAsync(order, default);

        Assert.Equal(near.Id, Assert.IsType<DispatchDecision.Assigned>(decision).Courier.Id);
        var (published, _) = Assert.Single(_publisher.Published);
        var assigned = Assert.IsType<CourierAssigned>(published);
        Assert.Equal(order.OrderId, assigned.OrderId);
        Assert.Equal(near.Id, assigned.CourierId);
        Assert.Equal([order.OrderId], (await _store.GetAsync(near.Id, default))!.ActiveOrderIds);
    }

    [Fact]
    public async Task Redelivered_event_reannounces_the_same_courier_with_same_message_id()
    {
        await AddCourierAsync("a", 0.003);
        await AddCourierAsync("b", 0.004);
        var order = ReadyOrder();

        await _service.DispatchAsync(order, default);
        await _service.DispatchAsync(order, default);

        var messages = _publisher.Published.ToArray();
        Assert.Equal(2, messages.Length);
        Assert.Equal(((CourierAssigned)messages[0].Event).CourierId, ((CourierAssigned)messages[1].Event).CourierId);
        Assert.Equal(messages[0].MessageId, messages[1].MessageId);
    }

    [Fact]
    public async Task Concurrent_orders_never_exceed_a_couriers_capacity()
    {
        var only = await AddCourierAsync("only", 0.002);
        var orders = Enumerable.Range(0, 5).Select(_ => ReadyOrder()).ToArray();

        var decisions = await Task.WhenAll(orders.Select(o => _service.DispatchAsync(o, default)));

        Assert.Single(decisions.OfType<DispatchDecision.Assigned>());
        Assert.Equal(4, decisions.OfType<DispatchDecision.Unassigned>().Count());
        Assert.Single((await _store.GetAsync(only.Id, default))!.ActiveOrderIds);
        Assert.Equal(4, (await _store.GetPendingAsync(default)).Count);
    }

    [Fact]
    public async Task Delivery_frees_capacity_and_pending_order_is_picked_up_on_retry()
    {
        var courier = await AddCourierAsync("solo", 0.002);
        var first = ReadyOrder();
        var second = ReadyOrder();
        await _service.DispatchAsync(first, default);
        await _service.DispatchAsync(second, default);

        await _service.ReleaseAsync(first.OrderId, default);
        var assigned = await _service.RetryPendingAsync(default);

        Assert.Equal(1, assigned);
        Assert.Equal([second.OrderId], (await _store.GetAsync(courier.Id, default))!.ActiveOrderIds);
        Assert.Empty(await _store.GetPendingAsync(default));
    }

    [Fact]
    public async Task Cancelled_pending_order_is_dropped()
    {
        var order = ReadyOrder();
        await _service.DispatchAsync(order, default);
        Assert.Single(await _store.GetPendingAsync(default));

        await _service.ReleaseAsync(order.OrderId, default);

        Assert.Empty(await _store.GetPendingAsync(default));
        Assert.Empty(_publisher.Published);
    }

    private async Task<Courier> AddCourierAsync(string name, double latitudeOffset)
    {
        var courier = new Courier
        {
            Id = Guid.NewGuid(),
            Name = name,
            Availability = CourierAvailability.Available,
            Location = new GeoPoint(Pickup.Latitude + latitudeOffset, Pickup.Longitude),
            LastSeenAt = Now.AddSeconds(-20),
        };
        await _store.SaveAsync(courier, default);
        return courier;
    }

    private static OrderReadyForPickup ReadyOrder() => new(
        Guid.NewGuid(), Guid.NewGuid(), "Luigi's Forno",
        Pickup.Latitude, Pickup.Longitude, 52.3600, 4.8900, Now);

    public void Dispose() => _service.Dispose();
}
