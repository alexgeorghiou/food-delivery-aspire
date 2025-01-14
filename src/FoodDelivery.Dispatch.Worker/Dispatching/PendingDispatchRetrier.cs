namespace FoodDelivery.Dispatch.Worker.Dispatching;

/// <summary>
/// Periodically retries orders that could not be matched (no courier online, all busy or
/// none in range). Courier location updates also trigger an immediate retry.
/// </summary>
public sealed partial class PendingDispatchRetrier(
    DispatchService dispatch,
    TimeProvider clock,
    ILogger<PendingDispatchRetrier> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var assigned = await dispatch.RetryPendingAsync(stoppingToken);
                if (assigned > 0)
                {
                    LogRetried(logger, assigned);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Assigned {Count} previously pending orders")]
    private static partial void LogRetried(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Pending dispatch retry failed")]
    private static partial void LogFailed(ILogger logger, Exception ex);
}
