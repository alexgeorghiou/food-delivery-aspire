using System.Text.Json;
using FoodDelivery.Domain.Dispatch;
using FoodDelivery.Domain.Events;
using FoodDelivery.Domain.Geo;
using FoodDelivery.Messaging;
using StackExchange.Redis;

namespace FoodDelivery.Dispatch.Worker.Couriers;

/// <summary>
/// Courier state in Redis:
/// <list type="bullet">
/// <item><c>dispatch:couriers</c> hash: courier id -> JSON snapshot</item>
/// <item><c>dispatch:couriers:geo</c> geo set: courier id -> last position (GEOADD / GEOSEARCH)</item>
/// <item><c>dispatch:assignments</c> hash: order id -> courier id</item>
/// <item><c>dispatch:pending</c> hash: order id -> OrderReadyForPickup awaiting a courier</item>
/// </list>
/// </summary>
public sealed class RedisCourierStore(IConnectionMultiplexer redis) : ICourierStore
{
    private const string CouriersKey = "dispatch:couriers";
    private const string GeoKey = "dispatch:couriers:geo";
    private const string AssignmentsKey = "dispatch:assignments";
    private const string PendingKey = "dispatch:pending";

    private static readonly JsonSerializerOptions Json = EventSerializer.Options;

    private IDatabase Db => redis.GetDatabase();

    public async Task<IReadOnlyList<Courier>> GetAllAsync(CancellationToken ct)
    {
        var entries = await Db.HashValuesAsync(CouriersKey);
        return entries.Select(Deserialize).OfType<Courier>().OrderBy(c => c.Name, StringComparer.Ordinal).ToArray();
    }

    public async Task<Courier?> GetAsync(Guid courierId, CancellationToken ct) =>
        Deserialize(await Db.HashGetAsync(CouriersKey, courierId.ToString()));

    public async Task<IReadOnlyList<Courier>> GetWithinAsync(GeoPoint center, double radiusKm, CancellationToken ct)
    {
        var hits = await Db.GeoSearchAsync(
            GeoKey,
            center.Longitude,
            center.Latitude,
            new GeoSearchCircle(radiusKm, GeoUnit.Kilometers),
            order: Order.Ascending);

        if (hits.Length == 0)
        {
            return [];
        }

        var values = await Db.HashGetAsync(CouriersKey, hits.Select(h => (RedisValue)h.Member.ToString()).ToArray());
        return values.Select(Deserialize).OfType<Courier>().ToArray();
    }

    public async Task SaveAsync(Courier courier, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(courier);
        var id = courier.Id.ToString();
        var tx = Db.CreateTransaction();
        _ = tx.HashSetAsync(CouriersKey, id, JsonSerializer.Serialize(courier, Json));
        if (courier.Location is { } location)
        {
            _ = tx.GeoAddAsync(GeoKey, location.Longitude, location.Latitude, id);
        }
        else
        {
            _ = tx.GeoRemoveAsync(GeoKey, id);
        }

        await tx.ExecuteAsync();
    }

    public async Task<Guid?> GetAssignmentAsync(Guid orderId, CancellationToken ct)
    {
        var value = await Db.HashGetAsync(AssignmentsKey, orderId.ToString());
        return value.HasValue && Guid.TryParse(value.ToString(), out var courierId) ? courierId : null;
    }

    public Task SaveAssignmentAsync(Guid orderId, Guid courierId, CancellationToken ct) =>
        Db.HashSetAsync(AssignmentsKey, orderId.ToString(), courierId.ToString());

    public Task RemoveAssignmentAsync(Guid orderId, CancellationToken ct) =>
        Db.HashDeleteAsync(AssignmentsKey, orderId.ToString());

    public Task AddPendingAsync(OrderReadyForPickup order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        return Db.HashSetAsync(PendingKey, order.OrderId.ToString(), JsonSerializer.Serialize(order, Json));
    }

    public async Task<IReadOnlyList<OrderReadyForPickup>> GetPendingAsync(CancellationToken ct)
    {
        var values = await Db.HashValuesAsync(PendingKey);
        return values
            .Where(v => v.HasValue)
            .Select(v => JsonSerializer.Deserialize<OrderReadyForPickup>(v.ToString(), Json))
            .OfType<OrderReadyForPickup>()
            .OrderBy(o => o.ReadyAt)
            .ToArray();
    }

    public Task RemovePendingAsync(Guid orderId, CancellationToken ct) =>
        Db.HashDeleteAsync(PendingKey, orderId.ToString());

    private static Courier? Deserialize(RedisValue value) =>
        value.HasValue ? JsonSerializer.Deserialize<Courier>(value.ToString(), Json) : null;
}
