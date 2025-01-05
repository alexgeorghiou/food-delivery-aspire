using FoodDelivery.Domain.Dispatch;
using FoodDelivery.Domain.Events;
using FoodDelivery.Domain.Geo;

namespace FoodDelivery.Dispatch.Worker.Couriers;

public interface ICourierStore
{
    Task<IReadOnlyList<Courier>> GetAllAsync(CancellationToken ct);

    Task<Courier?> GetAsync(Guid courierId, CancellationToken ct);

    /// <summary>
    /// Returns couriers whose last known position is within <paramref name="radiusKm"/> of
    /// <paramref name="center"/>. This is only a coarse pre-filter; ranking happens in the domain.
    /// </summary>
    Task<IReadOnlyList<Courier>> GetWithinAsync(GeoPoint center, double radiusKm, CancellationToken ct);

    Task SaveAsync(Courier courier, CancellationToken ct);

    Task<Guid?> GetAssignmentAsync(Guid orderId, CancellationToken ct);

    Task SaveAssignmentAsync(Guid orderId, Guid courierId, CancellationToken ct);

    Task RemoveAssignmentAsync(Guid orderId, CancellationToken ct);

    Task AddPendingAsync(OrderReadyForPickup order, CancellationToken ct);

    Task<IReadOnlyList<OrderReadyForPickup>> GetPendingAsync(CancellationToken ct);

    Task RemovePendingAsync(Guid orderId, CancellationToken ct);
}
