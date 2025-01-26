using System.Collections.Concurrent;
using FoodDelivery.Dispatch.Worker.Couriers;
using FoodDelivery.Domain.Dispatch;
using FoodDelivery.Domain.Events;
using FoodDelivery.Domain.Geo;
using FoodDelivery.Messaging;

namespace FoodDelivery.Services.Tests;

internal sealed class InMemoryCourierStore : ICourierStore
{
    private readonly ConcurrentDictionary<Guid, Courier> _couriers = new();
    private readonly ConcurrentDictionary<Guid, Guid> _assignments = new();
    private readonly ConcurrentDictionary<Guid, OrderReadyForPickup> _pending = new();

    public Task<IReadOnlyList<Courier>> GetAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Courier>>(_couriers.Values.ToArray());

    public Task<Courier?> GetAsync(Guid courierId, CancellationToken ct) =>
        Task.FromResult(_couriers.TryGetValue(courierId, out var c) ? c : null);

    public Task<IReadOnlyList<Courier>> GetWithinAsync(GeoPoint center, double radiusKm, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Courier>>(_couriers.Values
            .Where(c => c.Location is { } l && Haversine.DistanceKm(l, center) <= radiusKm)
            .ToArray());

    public Task SaveAsync(Courier courier, CancellationToken ct)
    {
        _couriers[courier.Id] = courier;
        return Task.CompletedTask;
    }

    public Task<Guid?> GetAssignmentAsync(Guid orderId, CancellationToken ct) =>
        Task.FromResult(_assignments.TryGetValue(orderId, out var id) ? id : (Guid?)null);

    public Task SaveAssignmentAsync(Guid orderId, Guid courierId, CancellationToken ct)
    {
        _assignments[orderId] = courierId;
        return Task.CompletedTask;
    }

    public Task RemoveAssignmentAsync(Guid orderId, CancellationToken ct)
    {
        _assignments.TryRemove(orderId, out _);
        return Task.CompletedTask;
    }

    public Task AddPendingAsync(OrderReadyForPickup order, CancellationToken ct)
    {
        _pending[order.OrderId] = order;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OrderReadyForPickup>> GetPendingAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OrderReadyForPickup>>(_pending.Values.OrderBy(o => o.ReadyAt).ToArray());

    public Task RemovePendingAsync(Guid orderId, CancellationToken ct)
    {
        _pending.TryRemove(orderId, out _);
        return Task.CompletedTask;
    }
}

internal sealed class RecordingPublisher : IEventPublisher
{
    public ConcurrentQueue<(IIntegrationEvent Event, Guid MessageId)> Published { get; } = new();

    public Task PublishAsync(IIntegrationEvent integrationEvent, Guid messageId, CancellationToken ct = default)
    {
        Published.Enqueue((integrationEvent, messageId));
        return Task.CompletedTask;
    }
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
