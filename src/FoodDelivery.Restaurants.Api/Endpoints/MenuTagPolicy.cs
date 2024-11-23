using Microsoft.AspNetCore.OutputCaching;

namespace FoodDelivery.Restaurants.Api.Endpoints;

/// <summary>
/// Tags a cached menu response with its restaurant id so a single menu can be evicted
/// when one of its items changes, without flushing every other restaurant's menu.
/// </summary>
internal sealed class MenuTagPolicy : IOutputCachePolicy
{
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        if (context.HttpContext.Request.RouteValues.TryGetValue("id", out var raw)
            && Guid.TryParse(raw?.ToString(), out var restaurantId))
        {
            context.Tags.Add(CachePolicies.MenuTag(restaurantId));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) => ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation) => ValueTask.CompletedTask;
}
