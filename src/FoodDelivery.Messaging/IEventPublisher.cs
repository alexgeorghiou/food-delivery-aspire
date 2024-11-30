using FoodDelivery.Domain.Events;

namespace FoodDelivery.Messaging;

public interface IEventPublisher
{
    /// <summary>Publishes an event and waits for the broker to confirm it.</summary>
    Task PublishAsync(IIntegrationEvent integrationEvent, Guid messageId, CancellationToken ct = default);
}
