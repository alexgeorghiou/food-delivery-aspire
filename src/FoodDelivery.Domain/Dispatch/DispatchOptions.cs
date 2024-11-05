namespace FoodDelivery.Domain.Dispatch;

public sealed record DispatchOptions
{
    /// <summary>Couriers further than this from the pickup are never considered.</summary>
    public double MaxPickupDistanceKm { get; init; } = 8.0;

    /// <summary>A courier whose last location ping is older than this is treated as offline.</summary>
    public TimeSpan LocationStaleAfter { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Candidates within this distance of the closest one are considered tied and are
    /// separated by load and idle time instead of raw distance.
    /// </summary>
    public double TieToleranceKm { get; init; } = 0.15;

    public static DispatchOptions Default { get; } = new();
}
