using System.Diagnostics;
using System.Diagnostics.Metrics;
using FoodDelivery.Dispatch.Worker.Couriers;
using FoodDelivery.Domain.Dispatch;
using FoodDelivery.Domain.Events;
using FoodDelivery.Domain.Geo;
using FoodDelivery.Messaging;

namespace FoodDelivery.Dispatch.Worker.Dispatching;

/// <summary>
/// Orchestrates dispatch: loads candidate couriers, asks <see cref="CourierMatcher"/> for a
/// decision, persists the assignment and announces it.
/// <para>
/// Decisions are serialised through a semaphore so two orders can never claim the last unit of
/// one courier's capacity. That is sufficient for a single worker replica; scaling out would need
/// a Redis lock or a Lua script doing the capacity check atomically.
/// </para>
/// </summary>
public sealed partial class DispatchService(
    ICourierStore store,
    IEventPublisher publisher,
    CourierMatcher matcher,
    DispatchOptions options,
    TimeProvider clock,
    ILogger<DispatchService> logger) : IDisposable
{
    public const string MeterName = "FoodDelivery.Dispatch";

    private static readonly ActivitySource ActivitySource = new("FoodDelivery.Dispatch");
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Assignments = Meter.CreateCounter<long>("dispatch.assignments");
    private static readonly Counter<long> Unassigned = Meter.CreateCounter<long>("dispatch.unassigned");
    private static readonly Histogram<double> PickupDistance = Meter.CreateHistogram<double>("dispatch.pickup_distance", "km");

    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<DispatchDecision> DispatchAsync(OrderReadyForPickup order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        using var activity = ActivitySource.StartActivity("dispatch order");
        activity?.SetTag("order.id", order.OrderId);

        await _gate.WaitAsync(ct);
        try
        {
            // Redelivered event: re-announce the existing assignment instead of picking again.
            if (await store.GetAssignmentAsync(order.OrderId, ct) is { } existingCourierId
                && await store.GetAsync(existingCourierId, ct) is { } existing)
            {
                var distance = existing.Location is { } loc ? Haversine.DistanceKm(loc, PickupOf(order)) : 0;
                await AnnounceAsync(order.OrderId, existing, distance, ct);
                return new DispatchDecision.Assigned(existing, distance, []);
            }

            var pickup = PickupOf(order);
            var candidates = await store.GetWithinAsync(pickup, options.MaxPickupDistanceKm, ct);
            var decision = matcher.Match(pickup, candidates, clock.GetUtcNow());

            switch (decision)
            {
                case DispatchDecision.Assigned assigned:
                    var updated = assigned.Courier.WithAssignment(order.OrderId, clock.GetUtcNow());
                    await store.SaveAsync(updated, ct);
                    await store.SaveAssignmentAsync(order.OrderId, updated.Id, ct);
                    await store.RemovePendingAsync(order.OrderId, ct);
                    await AnnounceAsync(order.OrderId, updated, assigned.DistanceKm, ct);

                    Assignments.Add(1);
                    PickupDistance.Record(assigned.DistanceKm);
                    activity?.SetTag("courier.id", updated.Id);
                    LogAssigned(logger, order.OrderId, updated.Name, assigned.DistanceKm, assigned.RankedCandidates.Count);
                    break;

                case DispatchDecision.Unassigned unassigned:
                    await store.AddPendingAsync(order, ct);
                    Unassigned.Add(1, new KeyValuePair<string, object?>("reason", unassigned.Reason.ToString()));
                    activity?.SetTag("dispatch.unassigned_reason", unassigned.Reason.ToString());
                    LogUnassigned(logger, order.OrderId, unassigned.Reason);
                    break;
            }

            return decision;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Frees the courier's capacity once the order leaves their hands.</summary>
    public async Task ReleaseAsync(Guid orderId, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await store.RemovePendingAsync(orderId, ct);
            if (await store.GetAssignmentAsync(orderId, ct) is not { } courierId)
            {
                return;
            }

            if (await store.GetAsync(courierId, ct) is { } courier)
            {
                await store.SaveAsync(courier.WithoutOrder(orderId), ct);
            }

            await store.RemoveAssignmentAsync(orderId, ct);
            LogReleased(logger, orderId, courierId);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Retries every order still waiting for a courier, oldest first.</summary>
    public async Task<int> RetryPendingAsync(CancellationToken ct)
    {
        var assigned = 0;
        foreach (var pending in await store.GetPendingAsync(ct))
        {
            if (await DispatchAsync(pending, ct) is DispatchDecision.Assigned)
            {
                assigned++;
            }
        }

        return assigned;
    }

    private Task AnnounceAsync(Guid orderId, Courier courier, double distanceKm, CancellationToken ct) =>
        publisher.PublishAsync(
            new CourierAssigned(orderId, courier.Id, courier.Name, Math.Round(distanceKm, 3), clock.GetUtcNow()),
            DeterministicMessageId(orderId, courier.Id),
            ct);

    private static GeoPoint PickupOf(OrderReadyForPickup order) => new(order.PickupLatitude, order.PickupLongitude);

    private static Guid DeterministicMessageId(Guid orderId, Guid courierId)
    {
        Span<byte> bytes = stackalloc byte[16];
        var a = orderId.ToByteArray();
        var b = courierId.ToByteArray();
        for (var i = 0; i < 16; i++)
        {
            bytes[i] = (byte)(a[i] ^ b[i]);
        }

        return new Guid(bytes);
    }

    public void Dispose() => _gate.Dispose();

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} -> {CourierName} at {DistanceKm:F2} km ({CandidateCount} candidates)")]
    private static partial void LogAssigned(ILogger logger, Guid orderId, string courierName, double distanceKm, int candidateCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No courier for order {OrderId}: {Reason}; queued for retry")]
    private static partial void LogUnassigned(ILogger logger, Guid orderId, NoCourierReason reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Released order {OrderId} from courier {CourierId}")]
    private static partial void LogReleased(ILogger logger, Guid orderId, Guid courierId);
}
