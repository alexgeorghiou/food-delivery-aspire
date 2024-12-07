using FoodDelivery.Domain.Events;
using FoodDelivery.Domain.Orders;
using FoodDelivery.Messaging;
using FoodDelivery.Orders.Api.Data;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace FoodDelivery.Orders.Api.Messaging;

/// <summary>
/// Applies courier assignments made by the dispatch worker to the order.
/// </summary>
public sealed partial class CourierAssignedConsumer(
    IConnection connection,
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<CourierAssignedConsumer> logger)
    : IntegrationEventConsumer(connection, logger)
{
    protected override string QueueName => "orders.courier-assigned";

    protected override IReadOnlyCollection<string> RoutingKeys => [EventRouting.CourierAssigned];

    protected override async Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken ct)
    {
        if (integrationEvent is not CourierAssigned assigned)
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var messageKey = $"courier-assigned:{assigned.OrderId:N}:{assigned.CourierId:N}";
        if (await db.ProcessedMessages.AnyAsync(m => m.MessageKey == messageKey, ct))
        {
            return;
        }

        var order = await db.Orders.Include(o => o.History).FirstOrDefaultAsync(o => o.Id == assigned.OrderId, ct);
        if (order is null)
        {
            LogUnknownOrder(logger, assigned.OrderId);
            return;
        }

        if (order.Status == OrderStatus.ReadyForPickup)
        {
            order.AssignCourier(assigned.CourierId, assigned.CourierName, clock.GetUtcNow());
            LogAssigned(logger, order.Id, assigned.CourierName, assigned.DistanceToPickupKm);
        }
        else
        {
            // e.g. the customer cancelled while dispatch was choosing a courier.
            LogIgnored(logger, order.Id, order.Status);
        }

        db.ProcessedMessages.Add(new ProcessedMessage { MessageKey = messageKey, ProcessedAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Courier assignment for unknown order {OrderId}")]
    private static partial void LogUnknownOrder(ILogger logger, Guid orderId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} assigned to {CourierName} ({DistanceKm:F2} km from pickup)")]
    private static partial void LogAssigned(ILogger logger, Guid orderId, string courierName, double distanceKm);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring courier assignment for order {OrderId} in status {Status}")]
    private static partial void LogIgnored(ILogger logger, Guid orderId, OrderStatus status);
}
