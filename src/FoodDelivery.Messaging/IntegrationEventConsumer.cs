using System.Diagnostics;
using FoodDelivery.Domain.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FoodDelivery.Messaging;

/// <summary>
/// Base class for a background consumer bound to one durable queue.
/// <para>
/// Delivery semantics are at-least-once: the message is acked only after
/// <see cref="HandleAsync"/> completes. A failure is retried once by requeueing; a second failure
/// (or an undeserialisable message) is rejected into the queue's dead-letter queue.
/// Handlers must therefore be idempotent.
/// </para>
/// </summary>
public abstract partial class IntegrationEventConsumer(IConnection connection, ILogger logger) : BackgroundService
{
    private IChannel? _channel;

    protected abstract string QueueName { get; }

    protected abstract IReadOnlyCollection<string> RoutingKeys { get; }

    protected virtual ushort PrefetchCount => 10;

    protected abstract Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await RabbitMqTopology.DeclareQueueAsync(_channel, QueueName, RoutingKeys, stoppingToken);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: PrefetchCount, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, delivery) => OnMessageAsync(_channel, delivery, stoppingToken);

        await _channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        var bindings = string.Join(", ", RoutingKeys);
        LogStarted(logger, QueueName, bindings);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task OnMessageAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken ct)
    {
        var parent = MessagingDiagnostics.ExtractTraceContext(delivery.BasicProperties.Headers);
        using var activity = MessagingDiagnostics.ActivitySource.StartActivity(
            $"{delivery.RoutingKey} process", ActivityKind.Consumer, parent);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", QueueName);
        activity?.SetTag("messaging.message.id", delivery.BasicProperties.MessageId);

        IIntegrationEvent integrationEvent;
        try
        {
            integrationEvent = EventSerializer.Deserialize(delivery.RoutingKey, delivery.Body.Span);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Text.Json.JsonException)
        {
            LogPoison(logger, ex, delivery.RoutingKey, delivery.BasicProperties.MessageId);
            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, ct);
            return;
        }

        try
        {
            await HandleAsync(integrationEvent, ct);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var requeue = !delivery.Redelivered;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogHandlerFailed(logger, ex, delivery.RoutingKey, integrationEvent.OrderId, requeue);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: requeue, ct);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Consuming {Queue} bound to [{RoutingKeys}]")]
    private static partial void LogStarted(ILogger logger, string queue, string routingKeys);

    [LoggerMessage(Level = LogLevel.Error, Message = "Rejecting undeserialisable {RoutingKey} message {MessageId}")]
    private static partial void LogPoison(ILogger logger, Exception ex, string routingKey, string? messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handler failed for {RoutingKey} (order {OrderId}); requeue={Requeue}")]
    private static partial void LogHandlerFailed(ILogger logger, Exception ex, string routingKey, Guid orderId, bool requeue);
}
