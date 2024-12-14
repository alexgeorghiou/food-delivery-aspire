namespace FoodDelivery.Orders.Api.Data;

public static partial class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
                await db.Database.EnsureCreatedAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < 10 && ex is not OperationCanceledException)
            {
                LogRetry(logger, attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Database not ready (attempt {Attempt}): {Error}")]
    private static partial void LogRetry(ILogger logger, int attempt, string error);
}
