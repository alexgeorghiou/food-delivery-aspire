using FoodDelivery.Domain.Contracts;
using FoodDelivery.Domain.Pricing;
using FoodDelivery.Restaurants.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace FoodDelivery.Restaurants.Api.Endpoints;

public static class MenuEndpoints
{
    public static IEndpointRouteBuilder MapMenuEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/restaurants/{id:guid}").WithTags("Menus");

        group.MapGet("/menu", GetMenu)
            .CacheOutput(CachePolicies.Menu)
            .WithName("GetMenu");

        group.MapPost("/menu/items", AddMenuItem).WithName("AddMenuItem");
        group.MapPut("/menu/items/{itemId:guid}", UpdateMenuItem).WithName("UpdateMenuItem");

        group.MapPost("/quote", Quote).WithName("QuoteCart");

        return app;
    }

    private static async Task<Results<Ok<MenuDto>, NotFound>> GetMenu(
        Guid id, RestaurantsDbContext db, bool? includeUnavailable, CancellationToken ct)
    {
        var restaurant = await db.Restaurants.AsNoTracking()
            .Include(r => r.MenuItems)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        return restaurant is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(restaurant.ToMenu(includeUnavailable == true));
    }

    private static async Task<Results<Created<MenuItemDto>, NotFound, ValidationProblem>> AddMenuItem(
        Guid id, UpsertMenuItemRequest request, RestaurantsDbContext db, IOutputCacheStore cache, CancellationToken ct)
    {
        if (Validate(request) is { } problem)
        {
            return problem;
        }

        if (!await db.Restaurants.AnyAsync(r => r.Id == id, ct))
        {
            return TypedResults.NotFound();
        }

        var item = new MenuItem
        {
            Id = Guid.NewGuid(),
            RestaurantId = id,
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
        };
        Apply(item, request);
        db.MenuItems.Add(item);
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CachePolicies.MenuTag(id), ct);

        return TypedResults.Created($"/restaurants/{id}/menu/items/{item.Id}", ToDto(item));
    }

    private static async Task<Results<Ok<MenuItemDto>, NotFound, ValidationProblem>> UpdateMenuItem(
        Guid id, Guid itemId, UpsertMenuItemRequest request, RestaurantsDbContext db, IOutputCacheStore cache, CancellationToken ct)
    {
        if (Validate(request) is { } problem)
        {
            return problem;
        }

        var item = await db.MenuItems.FirstOrDefaultAsync(m => m.Id == itemId && m.RestaurantId == id, ct);
        if (item is null)
        {
            return TypedResults.NotFound();
        }

        Apply(item, request);
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CachePolicies.MenuTag(id), ct);

        return TypedResults.Ok(ToDto(item));
    }

    /// <summary>
    /// Prices a basket against the current menu. The Orders API treats this as the
    /// authoritative price so clients can never set their own prices.
    /// </summary>
    private static async Task<Results<Ok<QuoteResponse>, NotFound, ValidationProblem>> Quote(
        Guid id, QuoteRequest request, RestaurantsDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (request.Lines is not { Count: > 0 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["lines"] = ["At least one line is required."] });
        }

        var restaurant = await db.Restaurants.AsNoTracking()
            .Include(r => r.MenuItems)
            .Include(r => r.OpeningPeriods)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (restaurant is null)
        {
            return TypedResults.NotFound();
        }

        var problems = new List<string>();
        var isOpen = restaurant.ToOpeningHours().IsOpenAt(clock.GetUtcNow(), restaurant.ResolveTimeZone());
        if (!isOpen)
        {
            problems.Add($"{restaurant.Name} is currently closed.");
        }

        var items = restaurant.MenuItems.ToDictionary(m => m.Id);
        var cart = new Cart(restaurant.Id);
        foreach (var line in request.Lines)
        {
            if (!items.TryGetValue(line.MenuItemId, out var item))
            {
                problems.Add($"Menu item {line.MenuItemId} does not exist.");
                continue;
            }

            if (!item.IsAvailable)
            {
                problems.Add($"{item.Name} is currently unavailable.");
                continue;
            }

            try
            {
                cart.Add(item.Id, item.Name, item.Price, line.Quantity);
            }
            catch (ArgumentOutOfRangeException)
            {
                problems.Add($"Quantity for {item.Name} must be between 1 and {CartLine.MaxQuantityPerLine}.");
            }
        }

        var totals = cart.CalculateTotals();
        return TypedResults.Ok(new QuoteResponse(
            restaurant.Id,
            restaurant.Name,
            restaurant.Latitude,
            restaurant.Longitude,
            isOpen,
            restaurant.Currency,
            cart.Lines.Select(l => new QuoteLine(l.MenuItemId, l.Name, l.UnitPrice, l.Quantity, l.LineTotal)).ToArray(),
            new QuoteTotals(totals.Subtotal, totals.DeliveryFee, totals.SmallOrderSurcharge, totals.ServiceFee, totals.Total),
            problems));
    }

    private static ValidationProblem? Validate(UpsertMenuItemRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Name is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            errors["category"] = ["Category is required."];
        }

        if (request.Price is < 0 or > 1000)
        {
            errors["price"] = ["Price must be between 0 and 1000."];
        }

        return errors.Count == 0 ? null : TypedResults.ValidationProblem(errors);
    }

    private static void Apply(MenuItem item, UpsertMenuItemRequest request)
    {
        item.Name = request.Name.Trim();
        item.Description = request.Description?.Trim() ?? string.Empty;
        item.Category = request.Category.Trim();
        item.Price = Money.Round(request.Price);
        item.IsAvailable = request.IsAvailable;
        item.IsVegetarian = request.IsVegetarian;
        item.IsSpicy = request.IsSpicy;
        item.SortOrder = request.SortOrder;
    }

    private static MenuItemDto ToDto(MenuItem i) =>
        new(i.Id, i.Name, i.Description, i.Price, i.IsAvailable, i.IsVegetarian, i.IsSpicy);
}
