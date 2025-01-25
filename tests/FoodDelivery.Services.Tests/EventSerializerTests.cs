using FoodDelivery.Domain.Events;
using FoodDelivery.Messaging;

namespace FoodDelivery.Services.Tests;

public class EventSerializerTests
{
    public static TheoryData<IIntegrationEvent> Events => new()
    {
        new OrderPlaced(Guid.NewGuid(), Guid.NewGuid(), 27.69m, DateTimeOffset.UnixEpoch),
        new OrderReadyForPickup(Guid.NewGuid(), Guid.NewGuid(), "Tokyo Bowl", 52.36, 4.89, 52.37, 4.90, DateTimeOffset.UnixEpoch),
        new OrderDelivered(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UnixEpoch),
        new OrderCancelled(Guid.NewGuid(), "Cancelled", "Too slow", DateTimeOffset.UnixEpoch),
        new CourierAssigned(Guid.NewGuid(), Guid.NewGuid(), "Sam", 0.42, DateTimeOffset.UnixEpoch),
    };

    [Theory]
    [MemberData(nameof(Events))]
    public void Every_event_round_trips_by_routing_key(IIntegrationEvent original)
    {
        var key = EventRouting.RoutingKeyFor(original);

        var restored = EventSerializer.Deserialize(key, EventSerializer.Serialize(original));

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Unknown_routing_key_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => EventSerializer.Deserialize("order.teleported", "{}"u8));
    }

    [Fact]
    public void Payload_uses_camel_case()
    {
        var json = EventSerializer.SerializeToString(new OrderDelivered(Guid.Empty, Guid.Empty, DateTimeOffset.UnixEpoch));
        Assert.Contains("\"orderId\"", json, StringComparison.Ordinal);
    }
}
