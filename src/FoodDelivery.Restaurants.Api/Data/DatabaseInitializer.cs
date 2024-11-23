using Microsoft.EntityFrameworkCore;

namespace FoodDelivery.Restaurants.Api.Data;

public static partial class DatabaseInitializer
{
    /// <summary>
    /// Creates the schema and seeds the demo catalogue on first start. Retries for a short
    /// while because the database container may still be warming up.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<RestaurantsDbContext>();
                await db.Database.EnsureCreatedAsync(cancellationToken);

                if (!await db.Restaurants.AnyAsync(cancellationToken))
                {
                    var seed = SeedData.Restaurants();
                    db.Restaurants.AddRange(seed);
                    await db.SaveChangesAsync(cancellationToken);
                    LogSeeded(logger, seed.Count);
                }

                return;
            }
            catch (Exception ex) when (attempt < 10 && ex is not OperationCanceledException)
            {
                LogRetry(logger, attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {Count} restaurants")]
    private static partial void LogSeeded(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Database not ready (attempt {Attempt}): {Error}")]
    private static partial void LogRetry(ILogger logger, int attempt, string error);
}
