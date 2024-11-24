using System.Text.Json;
using FoodDelivery.Domain.Events;

namespace FoodDelivery.Messaging;

/// <summary>
/// Maps routing keys to CLR types and handles JSON (de)serialisation of integration events.
/// The routing key doubles as the message type, so consumers never need assembly-qualified names.
/// </summary>
public static class EventSerializer
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, Type> TypesByRoutingKey = new(StringComparer.Ordinal)
    {
        [EventRouting.OrderPlaced] = typeof(OrderPlaced),
        [EventRouting.OrderReadyForPickup] = typeof(OrderReadyForPickup),
        [EventRouting.OrderDelivered] = typeof(OrderDelivered),
        [EventRouting.OrderCancelled] = typeof(OrderCancelled),
        [EventRouting.CourierAssigned] = typeof(CourierAssigned),
    };

    public static byte[] Serialize(IIntegrationEvent @event) =>
        JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), Options);

    public static string SerializeToString(IIntegrationEvent @event) =>
        JsonSerializer.Serialize(@event, @event.GetType(), Options);

    public static IIntegrationEvent Deserialize(string routingKey, ReadOnlySpan<byte> body)
    {
        if (!TypesByRoutingKey.TryGetValue(routingKey, out var type))
        {
            throw new InvalidOperationException($"No event type registered for routing key '{routingKey}'.");
        }

        return (IIntegrationEvent)(JsonSerializer.Deserialize(body, type, Options)
            ?? throw new InvalidOperationException($"Message for '{routingKey}' deserialised to null."));
    }
}
