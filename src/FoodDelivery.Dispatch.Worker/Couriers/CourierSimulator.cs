using FoodDelivery.Domain.Dispatch;
using FoodDelivery.Domain.Geo;

namespace FoodDelivery.Dispatch.Worker.Couriers;

/// <summary>
/// Development helper: registers a small fleet around central Amsterdam and sends a location
/// ping for each courier every few seconds, so the full order flow can be exercised without
/// a courier app. Enabled with <c>Dispatch:SimulateCouriers=true</c>.
/// </summary>
public sealed partial class CourierSimulator(
    ICourierStore store,
    TimeProvider clock,
    ILogger<CourierSimulator> logger) : BackgroundService
{
    private static readonly GeoPoint CityCentre = new(52.3702, 4.8952);
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(15);

    private static readonly (string Name, VehicleType Vehicle, double DLat, double DLon)[] Fleet =
    [
        ("Sam", VehicleType.Bicycle, 0.004, -0.006),
        ("Noor", VehicleType.Bicycle, -0.006, 0.004),
        ("Daan", VehicleType.Scooter, 0.010, 0.012),
        ("Lotte", VehicleType.Bicycle, -0.012, -0.010),
        ("Milan", VehicleType.Car, 0.018, -0.020),
        ("Emma", VehicleType.Scooter, -0.003, 0.025),
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var random = new Random(42);
        var ids = new List<Guid>();

        foreach (var (name, vehicle, dLat, dLon) in Fleet)
        {
            var id = new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("courier:" + name)).AsSpan(0, 16));
            ids.Add(id);
            var courier = await store.GetAsync(id, stoppingToken) ?? new Courier
            {
                Id = id,
                Name = name,
                Vehicle = vehicle,
                Capacity = Courier.DefaultCapacityFor(vehicle),
            };

            await store.SaveAsync(
                (courier with { Availability = CourierAvailability.Available })
                    .WithLocation(new GeoPoint(CityCentre.Latitude + dLat, CityCentre.Longitude + dLon), clock.GetUtcNow()),
                stoppingToken);
        }

        LogStarted(logger, ids.Count);

        using var timer = new PeriodicTimer(PingInterval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var id in ids)
            {
                if (await store.GetAsync(id, stoppingToken) is not { Location: { } location } courier)
                {
                    continue;
                }

                // Small random walk (~ up to 100 m per ping), pulled gently back towards the centre.
                var lat = location.Latitude + ((random.NextDouble() - 0.5) * 0.0018) + ((CityCentre.Latitude - location.Latitude) * 0.02);
                var lon = location.Longitude + ((random.NextDouble() - 0.5) * 0.0028) + ((CityCentre.Longitude - location.Longitude) * 0.02);
                await store.SaveAsync(courier.WithLocation(new GeoPoint(lat, lon), clock.GetUtcNow()), stoppingToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Courier simulator running with {Count} couriers")]
    private static partial void LogStarted(ILogger logger, int count);
}
