using FoodDelivery.Domain.Geo;
using FoodDelivery.Domain.Menus;
using FoodDelivery.Restaurants.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace FoodDelivery.Restaurants.Api.Endpoints;

public static class RestaurantEndpoints
{
    public static IEndpointRouteBuilder MapRestaurantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/restaurants").WithTags("Restaurants");

        group.MapGet("/", ListRestaurants)
            .CacheOutput(CachePolicies.Listing)
            .WithName("ListRestaurants");

        group.MapGet("/{id:guid}", GetRestaurant).WithName("GetRestaurant");

        group.MapPut("/{id:guid}/opening-hours", ReplaceOpeningHours).WithName("ReplaceOpeningHours");

        return app;
    }

    private static async Task<Ok<RestaurantSummary[]>> ListRestaurants(
        RestaurantsDbContext db,
        TimeProvider clock,
        string? cuisine,
        double? lat,
        double? lon,
        bool? openNow,
        CancellationToken ct)
    {
        var query = db.Restaurants.AsNoTracking().Include(r => r.OpeningPeriods).AsQueryable();
        if (!string.IsNullOrWhiteSpace(cuisine))
        {
            query = query.Where(r => r.Cuisine == cuisine);
        }

        var restaurants = await query.ToListAsync(ct);
        GeoPoint? origin = lat is { } la && lon is { } lo ? new GeoPoint(la, lo) : null;
        var now = clock.GetUtcNow();

        var summaries = restaurants
            .Select(r => r.ToSummary(now, origin))
            .Where(s => openNow != true || s.IsOpen)
            .OrderByDescending(s => s.IsOpen)
            .ThenBy(s => s.DistanceKm ?? 0)
            .ThenBy(s => s.Name, StringComparer.Ordinal)
            .ToArray();

        return TypedResults.Ok(summaries);
    }

    private static async Task<Results<Ok<RestaurantDetails>, NotFound>> GetRestaurant(
        Guid id, RestaurantsDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var restaurant = await db.Restaurants.AsNoTracking()
            .Include(r => r.OpeningPeriods)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (restaurant is null)
        {
            return TypedResults.NotFound();
        }

        var periods = restaurant.OpeningPeriods
            .OrderBy(p => p.Day).ThenBy(p => p.Opens)
            .Select(p => new OpeningPeriodDto(p.Day, p.Opens, p.Closes))
            .ToArray();

        return TypedResults.Ok(new RestaurantDetails(restaurant.ToSummary(clock.GetUtcNow()), restaurant.TimeZoneId, periods));
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> ReplaceOpeningHours(
        Guid id,
        ReplaceOpeningHoursRequest request,
        RestaurantsDbContext db,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        try
        {
            // Validates overlap rules before anything touches the database.
            _ = new OpeningHours(request.Periods.Select(p => new OpeningPeriod(p.Day, p.Opens, p.Closes)));
        }
        catch (ArgumentException ex)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["periods"] = [ex.Message] });
        }

        var restaurant = await db.Restaurants.Include(r => r.OpeningPeriods).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (restaurant is null)
        {
            return TypedResults.NotFound();
        }

        restaurant.OpeningPeriods.Clear();
        restaurant.OpeningPeriods.AddRange(request.Periods.Select(p => new OpeningPeriodRow
        {
            RestaurantId = id,
            Day = p.Day,
            Opens = p.Opens,
            Closes = p.Closes,
        }));

        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CachePolicies.ListingTag, ct);
        return TypedResults.NoContent();
    }
}
