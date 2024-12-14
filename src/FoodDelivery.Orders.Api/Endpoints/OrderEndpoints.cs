using FoodDelivery.Domain.Contracts;
using FoodDelivery.Domain.Geo;
using FoodDelivery.Domain.Orders;
using FoodDelivery.Domain.Pricing;
using FoodDelivery.Orders.Api.Clients;
using FoodDelivery.Orders.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodDelivery.Orders.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").WithTags("Orders");

        group.MapPost("/", PlaceOrder).WithName("PlaceOrder");
        group.MapGet("/", ListOrders).WithName("ListOrders");
        group.MapGet("/{id:guid}", GetOrder).WithName("GetOrder");
        group.MapPost("/{id:guid}/status", ChangeStatus).WithName("ChangeOrderStatus");
        group.MapPost("/{id:guid}/cancel", Cancel).WithName("CancelOrder");

        return app;
    }

    private static async Task<Results<Created<OrderDto>, NotFound<ProblemDetails>, ValidationProblem>> PlaceOrder(
        PlaceOrderRequest request,
        RestaurantsClient restaurants,
        OrdersDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var quote = await restaurants.QuoteAsync(
            request.RestaurantId,
            new QuoteRequest(request.Lines.Select(l => new QuoteRequestLine(l.MenuItemId, l.Quantity)).ToArray()),
            ct);

        if (quote is null)
        {
            return TypedResults.NotFound(new ProblemDetails { Title = "Restaurant not found" });
        }

        if (quote.Problems.Count > 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["order"] = [.. quote.Problems] });
        }

        var order = Order.Place(
            new RestaurantSnapshot(quote.RestaurantId, quote.RestaurantName, new GeoPoint(quote.Latitude, quote.Longitude)),
            request.CustomerName,
            request.DeliveryAddress,
            new GeoPoint(request.DeliveryLatitude, request.DeliveryLongitude),
            quote.Lines.Select(l => new CartLine(l.MenuItemId, l.Name, l.UnitPrice, l.Quantity)).ToArray(),
            PricingPolicy.Default,
            clock.GetUtcNow());

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created($"/orders/{order.Id}", order.ToDto());
    }

    private static async Task<Ok<OrderSummaryDto[]>> ListOrders(
        OrdersDbContext db, Guid? restaurantId, OrderStatus? status, int? take, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking();
        if (restaurantId is { } r)
        {
            query = query.Where(o => o.RestaurantId == r);
        }

        if (status is { } s)
        {
            query = query.Where(o => o.Status == s);
        }

        var orders = await query
            .OrderByDescending(o => o.PlacedAt)
            .Take(Math.Clamp(take ?? 50, 1, 200))
            .Select(o => new OrderSummaryDto(o.Id, o.RestaurantName, o.CustomerName, o.Status, o.Total, o.PlacedAt))
            .ToArrayAsync(ct);

        return TypedResults.Ok(orders);
    }

    private static async Task<Results<Ok<OrderDto>, NotFound>> GetOrder(Guid id, OrdersDbContext db, CancellationToken ct)
    {
        var order = await LoadAsync(db, id, ct);
        return order is null ? TypedResults.NotFound() : TypedResults.Ok(order.ToDto());
    }

    private static async Task<Results<Ok<OrderDto>, NotFound, Conflict<ProblemDetails>>> ChangeStatus(
        Guid id, ChangeStatusRequest request, OrdersDbContext db, TimeProvider clock, CancellationToken ct)
    {
        // In a real deployment the actor comes from the caller's identity, not the request body.
        if (request.Status == OrderStatus.CourierAssigned)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Courier assignment is automatic",
                Detail = "Couriers are assigned by the dispatch service once the order is ready for pickup.",
            });
        }

        return await ApplyAsync(db, id, order => order.TransitionTo(request.Status, request.Actor, clock.GetUtcNow(), request.Note), ct);
    }

    private static Task<Results<Ok<OrderDto>, NotFound, Conflict<ProblemDetails>>> Cancel(
        Guid id, OrdersDbContext db, TimeProvider clock, CancellationToken ct) =>
        ApplyAsync(db, id, order => order.TransitionTo(OrderStatus.Cancelled, OrderActor.Customer, clock.GetUtcNow(), "Cancelled by customer"), ct);

    private static async Task<Results<Ok<OrderDto>, NotFound, Conflict<ProblemDetails>>> ApplyAsync(
        OrdersDbContext db, Guid id, Action<Order> change, CancellationToken ct)
    {
        var order = await LoadAsync(db, id, ct, tracking: true);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            change(order);
            await db.SaveChangesAsync(ct);
        }
        catch (InvalidOrderTransitionException ex)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Invalid status change",
                Detail = ex.Message,
                Extensions = { ["currentStatus"] = ex.From.ToString() },
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Order was modified concurrently",
                Detail = "Reload the order and try again.",
            });
        }

        return TypedResults.Ok(order.ToDto());
    }

    private static Task<Order?> LoadAsync(OrdersDbContext db, Guid id, CancellationToken ct, bool tracking = false)
    {
        var query = db.Orders.Include(o => o.Lines).Include(o => o.History).AsSplitQuery();
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    private static Dictionary<string, string[]> Validate(PlaceOrderRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.RestaurantId == Guid.Empty)
        {
            errors["restaurantId"] = ["Restaurant is required."];
        }

        if (string.IsNullOrWhiteSpace(request.CustomerName) || request.CustomerName.Length > 120)
        {
            errors["customerName"] = ["Customer name is required (max 120 characters)."];
        }

        if (string.IsNullOrWhiteSpace(request.DeliveryAddress) || request.DeliveryAddress.Length > 250)
        {
            errors["deliveryAddress"] = ["Delivery address is required (max 250 characters)."];
        }

        if (request.DeliveryLatitude is < -90 or > 90 || request.DeliveryLongitude is < -180 or > 180)
        {
            errors["deliveryLocation"] = ["Delivery coordinates are out of range."];
        }

        if (request.Lines is not { Count: > 0 })
        {
            errors["lines"] = ["At least one item is required."];
        }

        return errors;
    }
}
