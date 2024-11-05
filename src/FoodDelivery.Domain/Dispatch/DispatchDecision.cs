namespace FoodDelivery.Domain.Dispatch;

public sealed record CourierCandidate(Courier Courier, double DistanceKm);

public enum NoCourierReason
{
    NoCouriersOnline,
    AllAtCapacity,
    NoneInRange,
}

public abstract record DispatchDecision
{
    public sealed record Assigned(Courier Courier, double DistanceKm, IReadOnlyList<CourierCandidate> RankedCandidates)
        : DispatchDecision;

    public sealed record Unassigned(NoCourierReason Reason) : DispatchDecision;
}
