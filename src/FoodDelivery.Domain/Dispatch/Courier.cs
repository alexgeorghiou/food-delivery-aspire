using FoodDelivery.Domain.Geo;

namespace FoodDelivery.Domain.Dispatch;

public enum CourierAvailability
{
    Offline = 0,
    Available = 1,
    OnBreak = 2,
}

public enum VehicleType
{
    Bicycle = 0,
    Scooter = 1,
    Car = 2,
}

/// <summary>
/// Snapshot of a courier as seen by dispatch. Immutable: updates produce a new instance,
/// which keeps the store implementations simple (replace the whole value).
/// </summary>
public sealed record Courier
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public VehicleType Vehicle { get; init; } = VehicleType.Bicycle;

    public CourierAvailability Availability { get; init; } = CourierAvailability.Offline;

    public GeoPoint? Location { get; init; }

    public DateTimeOffset? LastSeenAt { get; init; }

    /// <summary>Maximum number of orders the courier can carry at once.</summary>
    public int Capacity { get; init; } = 1;

    public IReadOnlyList<Guid> ActiveOrderIds { get; init; } = [];

    /// <summary>When the courier last received an assignment; used to spread work fairly.</summary>
    public DateTimeOffset? LastAssignedAt { get; init; }

    public int ActiveOrderCount => ActiveOrderIds.Count;

    public bool HasSpareCapacity => ActiveOrderCount < Capacity;

    public static int DefaultCapacityFor(VehicleType vehicle) => vehicle switch
    {
        VehicleType.Car => 3,
        VehicleType.Scooter => 2,
        _ => 1,
    };

    public Courier WithLocation(GeoPoint location, DateTimeOffset seenAt) =>
        this with { Location = location, LastSeenAt = seenAt };

    public Courier WithAssignment(Guid orderId, DateTimeOffset at)
    {
        if (ActiveOrderIds.Contains(orderId))
        {
            return this;
        }

        if (!HasSpareCapacity)
        {
            throw new InvalidOperationException($"Courier {Name} is already at capacity ({Capacity}).");
        }

        return this with { ActiveOrderIds = [.. ActiveOrderIds, orderId], LastAssignedAt = at };
    }

    public Courier WithoutOrder(Guid orderId) =>
        this with { ActiveOrderIds = ActiveOrderIds.Where(id => id != orderId).ToArray() };
}
