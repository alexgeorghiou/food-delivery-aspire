using FoodDelivery.Dispatch.Worker.Dispatching;
using FoodDelivery.Domain.Dispatch;
using FoodDelivery.Domain.Geo;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FoodDelivery.Dispatch.Worker.Couriers;

public sealed record UpsertCourierRequest(string Name, VehicleType Vehicle, CourierAvailability Availability, int? Capacity);

public sealed record LocationUpdate(double Latitude, double Longitude);

public sealed record CourierView(
    Guid Id,
    string Name,
    VehicleType Vehicle,
    CourierAvailability Availability,
    double? Latitude,
    double? Longitude,
    DateTimeOffset? LastSeenAt,
    int Capacity,
    IReadOnlyList<Guid> ActiveOrderIds);

public static class CourierEndpoints
{
    public static IEndpointRouteBuilder MapCourierEndpoints(this IEndpointRouteBuilder app)
    {
        var couriers = app.MapGroup("/couriers").WithTags("Couriers");

        couriers.MapGet("/", async (ICourierStore store, CancellationToken ct) =>
            TypedResults.Ok((await store.GetAllAsync(ct)).Select(ToView).ToArray()));

        couriers.MapGet("/{id:guid}", async Task<Results<Ok<CourierView>, NotFound>> (Guid id, ICourierStore store, CancellationToken ct) =>
            await store.GetAsync(id, ct) is { } courier ? TypedResults.Ok(ToView(courier)) : TypedResults.NotFound());

        couriers.MapPut("/{id:guid}", UpsertCourier);
        couriers.MapPost("/{id:guid}/location", UpdateLocation);

        app.MapGet("/dispatch/pending", async (ICourierStore store, CancellationToken ct) =>
            TypedResults.Ok(await store.GetPendingAsync(ct))).WithTags("Dispatch");

        return app;
    }

    private static async Task<Results<Ok<CourierView>, ValidationProblem>> UpsertCourier(
        Guid id, UpsertCourierRequest request, ICourierStore store, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Name is required."] });
        }

        if (request.Capacity is < 1 or > 5)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["capacity"] = ["Capacity must be between 1 and 5."] });
        }

        var existing = await store.GetAsync(id, ct);
        var courier = (existing ?? new Courier { Id = id, Name = request.Name }) with
        {
            Name = request.Name.Trim(),
            Vehicle = request.Vehicle,
            Availability = request.Availability,
            Capacity = request.Capacity ?? Courier.DefaultCapacityFor(request.Vehicle),
        };

        await store.SaveAsync(courier, ct);
        return TypedResults.Ok(ToView(courier));
    }

    private static async Task<Results<Accepted, NotFound, ValidationProblem>> UpdateLocation(
        Guid id,
        LocationUpdate update,
        ICourierStore store,
        DispatchService dispatch,
        TimeProvider clock,
        CancellationToken ct)
    {
        GeoPoint point;
        try
        {
            point = new GeoPoint(update.Latitude, update.Longitude);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "location"] = [ex.Message] });
        }

        if (await store.GetAsync(id, ct) is not { } courier)
        {
            return TypedResults.NotFound();
        }

        await store.SaveAsync(courier.WithLocation(point, clock.GetUtcNow()), ct);

        // A courier coming into range may unblock a waiting order.
        if (courier.Availability == CourierAvailability.Available && courier.HasSpareCapacity)
        {
            await dispatch.RetryPendingAsync(ct);
        }

        return TypedResults.Accepted((string?)null);
    }

    private static CourierView ToView(Courier c) => new(
        c.Id, c.Name, c.Vehicle, c.Availability,
        c.Location?.Latitude, c.Location?.Longitude, c.LastSeenAt,
        c.Capacity, c.ActiveOrderIds);
}
