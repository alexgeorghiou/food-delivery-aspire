using FoodDelivery.Domain.Geo;

namespace FoodDelivery.Domain.Dispatch;

/// <summary>
/// Picks the nearest available courier for a pickup.
/// <para>
/// Filtering: the courier must be <see cref="CourierAvailability.Available"/>, have a fresh
/// location fix, spare capacity and be within <see cref="DispatchOptions.MaxPickupDistanceKm"/>.
/// </para>
/// <para>
/// Ranking: by haversine distance to the pickup. Everyone within
/// <see cref="DispatchOptions.TieToleranceKm"/> of the closest courier forms a tie group, which is
/// ordered by fewest active orders, then longest time since last assignment, then id (so the
/// result is fully deterministic).
/// </para>
/// </summary>
public sealed class CourierMatcher
{
    private readonly DispatchOptions _options;

    public CourierMatcher(DispatchOptions? options = null)
    {
        _options = options ?? DispatchOptions.Default;
    }

    public DispatchDecision Match(GeoPoint pickup, IEnumerable<Courier> couriers, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(couriers);

        var online = couriers
            .Where(c => c.Availability == CourierAvailability.Available)
            .Where(c => c.Location is not null && c.LastSeenAt is { } seen && now - seen <= _options.LocationStaleAfter)
            .ToArray();

        if (online.Length == 0)
        {
            return new DispatchDecision.Unassigned(NoCourierReason.NoCouriersOnline);
        }

        var withCapacity = online.Where(c => c.HasSpareCapacity).ToArray();
        if (withCapacity.Length == 0)
        {
            return new DispatchDecision.Unassigned(NoCourierReason.AllAtCapacity);
        }

        var inRange = withCapacity
            .Select(c => new CourierCandidate(c, Haversine.DistanceKm(c.Location!.Value, pickup)))
            .Where(c => c.DistanceKm <= _options.MaxPickupDistanceKm)
            .ToArray();

        if (inRange.Length == 0)
        {
            return new DispatchDecision.Unassigned(NoCourierReason.NoneInRange);
        }

        var ranked = Rank(inRange);
        var best = ranked[0];
        return new DispatchDecision.Assigned(best.Courier, best.DistanceKm, ranked);
    }

    private List<CourierCandidate> Rank(IReadOnlyCollection<CourierCandidate> candidates)
    {
        var closest = candidates.Min(c => c.DistanceKm);
        var cutoff = closest + _options.TieToleranceKm;

        var tieGroup = candidates
            .Where(c => c.DistanceKm <= cutoff)
            .OrderBy(c => c.Courier.ActiveOrderCount)
            .ThenBy(c => c.Courier.LastAssignedAt ?? DateTimeOffset.MinValue)
            .ThenBy(c => c.DistanceKm)
            .ThenBy(c => c.Courier.Id);

        var rest = candidates
            .Where(c => c.DistanceKm > cutoff)
            .OrderBy(c => c.DistanceKm)
            .ThenBy(c => c.Courier.Id);

        return [.. tieGroup, .. rest];
    }
}
