using FoodDelivery.Messaging;
using FoodDelivery.Orders.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FoodDelivery.Orders.Api.Messaging;

/// <summary>
/// Relays committed outbox rows to RabbitMQ in order of occurrence. Publishing is
/// at-least-once: if the process dies between publish and marking the row, the event is sent
/// again and consumers de-duplicate on message id.
/// </summary>
public sealed partial class OutboxPublisher(
    IServiceScopeFactory scopes,
    IEventPublisher publisher,
    TimeProvider clock,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private const int MaxAttempts = 20;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval, clock);
        do
        {
            try
            {
                while (await PublishBatchAsync(stoppingToken) == BatchSize)
                {
                    // Keep draining while full batches come back.
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogBatchFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task<int> PublishBatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var pending = await db.Outbox
            .Where(m => m.PublishedAt == null && m.Attempts < MaxAttempts)
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var message in pending)
        {
            try
            {
                var integrationEvent = EventSerializer.Deserialize(message.RoutingKey, System.Text.Encoding.UTF8.GetBytes(message.Payload));
                await publisher.PublishAsync(integrationEvent, message.Id, ct);
                message.PublishedAt = clock.GetUtcNow();
                LogPublished(logger, message.RoutingKey, message.OrderId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                LogPublishFailed(logger, ex, message.RoutingKey, message.OrderId, message.Attempts);

                // Preserve per-order ordering: stop the batch rather than skipping ahead.
                break;
            }
        }

        await db.SaveChangesAsync(ct);
        return pending.Count;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Published {RoutingKey} for order {OrderId}")]
    private static partial void LogPublished(ILogger logger, string routingKey, Guid orderId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to publish {RoutingKey} for order {OrderId} (attempt {Attempt})")]
    private static partial void LogPublishFailed(ILogger logger, Exception ex, string routingKey, Guid orderId, int attempt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox batch failed")]
    private static partial void LogBatchFailed(ILogger logger, Exception ex);
}
