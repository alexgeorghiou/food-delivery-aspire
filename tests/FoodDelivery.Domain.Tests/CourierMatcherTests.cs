using FoodDelivery.Domain.Dispatch;
using FoodDelivery.Domain.Geo;

namespace FoodDelivery.Domain.Tests;

public class CourierMatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 18, 0, 0, TimeSpan.Zero);

    // Pickup in central Amsterdam. 0.001 deg latitude ~ 111 m.
    private static readonly GeoPoint Pickup = new(52.3700, 4.8900);

    private readonly CourierMatcher _matcher = new();

    [Fact]
    public void Picks_the_nearest_available_courier()
    {
        var near = CourierAt("near", 0.005);
        var far = CourierAt("far", 0.020);

        var decision = _matcher.Match(Pickup, [far, near], Now);

        var assigned = Assert.IsType<DispatchDecision.Assigned>(decision);
        Assert.Equal(near.Id, assigned.Courier.Id);
        Assert.InRange(assigned.DistanceKm, 0.5, 0.6);
        Assert.Equal([near.Id, far.Id], assigned.RankedCandidates.Select(c => c.Courier.Id));
    }

    [Fact]
    public void Skips_couriers_that_are_offline_or_on_break()
    {
        var offline = CourierAt("offline", 0.001) with { Availability = CourierAvailability.Offline };
        var onBreak = CourierAt("break", 0.002) with { Availability = CourierAvailability.OnBreak };
        var available = CourierAt("available", 0.030);

        var assigned = Assert.IsType<DispatchDecision.Assigned>(_matcher.Match(Pickup, [offline, onBreak, available], Now));
        Assert.Equal(available.Id, assigned.Courier.Id);
    }

    [Fact]
    public void Skips_couriers_with_stale_location()
    {
        var stale = CourierAt("stale", 0.001) with { LastSeenAt = Now.AddMinutes(-6) };
        var fresh = CourierAt("fresh", 0.010);

        var assigned = Assert.IsType<DispatchDecision.Assigned>(_matcher.Match(Pickup, [stale, fresh], Now));
        Assert.Equal(fresh.Id, assigned.Courier.Id);
    }

    [Fact]
    public void Skips_couriers_at_capacity()
    {
        var full = CourierAt("full", 0.001).WithAssignment(Guid.NewGuid(), Now.AddMinutes(-3));
        var free = CourierAt("free", 0.010);

        var assigned = Assert.IsType<DispatchDecision.Assigned>(_matcher.Match(Pickup, [full, free], Now));
        Assert.Equal(free.Id, assigned.Courier.Id);
    }

    [Fact]
    public void Couriers_with_spare_capacity_can_take_a_second_order()
    {
        var car = CourierAt("car", 0.001, capacity: 3).WithAssignment(Guid.NewGuid(), Now.AddMinutes(-3));

        var assigned = Assert.IsType<DispatchDecision.Assigned>(_matcher.Match(Pickup, [car], Now));
        Assert.Equal(car.Id, assigned.Courier.Id);
    }

    [Fact]
    public void Tie_within_tolerance_prefers_the_less_loaded_courier()
    {
        var busy = CourierAt("busy", 0.0050, capacity: 2).WithAssignment(Guid.NewGuid(), Now.AddMinutes(-10));
        var idle = CourierAt("idle", 0.0055, capacity: 2);

        var assigned = Assert.IsType<DispatchDecision.Assigned>(_matcher.Match(Pickup, [busy, idle], Now));
        Assert.Equal(idle.Id, assigned.Courier.Id);
    }

    [Fact]
    public void Tie_with_equal_load_prefers_the_courier_idle_longest()
    {
        var recent = CourierAt("recent", 0.0050) with { LastAssignedAt = Now.AddMinutes(-5) };
        var waiting = CourierAt("waiting", 0.0052) with { LastAssignedAt = Now.AddMinutes(-45) };

        var assigned = Assert.IsType<DispatchDecision.Assigned>(_matcher.Match(Pickup, [recent, waiting], Now));
        Assert.Equal(waiting.Id, assigned.Courier.Id);
    }

    [Fact]
    public void Never_assigned_courier_wins_a_tie_over_recently_assigned()
    {
        var recent = CourierAt("recent", 0.0050) with { LastAssignedAt = Now.AddMinutes(-5) };
        var fresh = CourierAt("fresh", 0.0051);

        var assigned = Assert.IsType<DispatchDecision.Assigned>(_matcher.Match(Pickup, [recent, fresh], Now));
        Assert.Equal(fresh.Id, assigned.Courier.Id);
    }

    [Fact]
    public void Outside_tie_tolerance_distance_wins_over_load()
    {
        var busyButClose = CourierAt("close", 0.002, capacity: 2).WithAssignment(Guid.NewGuid(), Now);
        var idleButFar = CourierAt("far", 0.010, capacity: 2);

        var assigned = Assert.IsType<DispatchDecision.Assigned>(_matcher.Match(Pickup, [idleButFar, busyButClose], Now));
        Assert.Equal(busyButClose.Id, assigned.Courier.Id);
    }

    [Fact]
    public void Exact_tie_is_broken_deterministically_by_id()
    {
        var a = CourierAt("a", 0.005) with { Id = Guid.Parse("00000000-0000-0000-0000-00000000000a") };
        var b = CourierAt("b", 0.005) with { Id = Guid.Parse("00000000-0000-0000-0000-00000000000b") };

        var first = Assert.IsType<DispatchDecision.Assigned>(_matcher.Match(Pickup, [b, a], Now));
        var second = Assert.IsType<DispatchDecision.Assigned>(_matcher.Match(Pickup, [a, b], Now));

        Assert.Equal(a.Id, first.Courier.Id);
        Assert.Equal(a.Id, second.Courier.Id);
    }

    [Fact]
    public void Reports_no_couriers_online()
    {
        var decision = _matcher.Match(Pickup, [CourierAt("off", 0.001) with { Availability = CourierAvailability.Offline }], Now);

        Assert.Equal(NoCourierReason.NoCouriersOnline, Assert.IsType<DispatchDecision.Unassigned>(decision).Reason);
    }

    [Fact]
    public void Reports_all_at_capacity()
    {
        var full = CourierAt("full", 0.001).WithAssignment(Guid.NewGuid(), Now);

        var decision = _matcher.Match(Pickup, [full], Now);

        Assert.Equal(NoCourierReason.AllAtCapacity, Assert.IsType<DispatchDecision.Unassigned>(decision).Reason);
    }

    [Fact]
    public void Reports_none_in_range()
    {
        var tooFar = CourierAt("far", 0.1); // ~11 km

        var decision = _matcher.Match(Pickup, [tooFar], Now);

        Assert.Equal(NoCourierReason.NoneInRange, Assert.IsType<DispatchDecision.Unassigned>(decision).Reason);
    }

    [Fact]
    public void Max_distance_is_configurable()
    {
        var matcher = new CourierMatcher(DispatchOptions.Default with { MaxPickupDistanceKm = 15 });

        Assert.IsType<DispatchDecision.Assigned>(matcher.Match(Pickup, [CourierAt("far", 0.1)], Now));
    }

    [Fact]
    public void Assignment_is_idempotent_per_order_and_respects_capacity()
    {
        var order = Guid.NewGuid();
        var courier = CourierAt("c", 0.001);

        var once = courier.WithAssignment(order, Now);
        var twice = once.WithAssignment(order, Now);

        Assert.Equal(1, twice.ActiveOrderCount);
        Assert.Throws<InvalidOperationException>(() => twice.WithAssignment(Guid.NewGuid(), Now));
        Assert.Equal(0, twice.WithoutOrder(order).ActiveOrderCount);
    }

    [Theory]
    [InlineData(VehicleType.Bicycle, 1)]
    [InlineData(VehicleType.Scooter, 2)]
    [InlineData(VehicleType.Car, 3)]
    public void Default_capacity_depends_on_vehicle(VehicleType vehicle, int expected)
    {
        Assert.Equal(expected, Courier.DefaultCapacityFor(vehicle));
    }

    private static Courier CourierAt(string name, double latitudeOffset, int capacity = 1) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Availability = CourierAvailability.Available,
        Location = new GeoPoint(Pickup.Latitude + latitudeOffset, Pickup.Longitude),
        LastSeenAt = Now.AddSeconds(-30),
        Capacity = capacity,
    };
}
