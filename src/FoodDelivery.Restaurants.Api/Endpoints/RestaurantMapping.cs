using FoodDelivery.Domain.Geo;
using FoodDelivery.Restaurants.Api.Data;

namespace FoodDelivery.Restaurants.Api.Endpoints;

internal static class RestaurantMapping
{
    public static RestaurantSummary ToSummary(this Restaurant restaurant, DateTimeOffset now, GeoPoint? origin = null)
    {
        var tz = restaurant.ResolveTimeZone();
        var hours = restaurant.ToOpeningHours();
        var local = TimeZoneInfo.ConvertTime(now, tz).DateTime;
        var isOpen = hours.IsOpenAt(local);

        DateTimeOffset? nextOpening = null;
        if (!isOpen && hours.NextOpening(local) is { } nextLocal)
        {
            nextOpening = new DateTimeOffset(nextLocal, tz.GetUtcOffset(nextLocal));
        }

        double? distance = origin is { } o ? Math.Round(Haversine.DistanceKm(o, restaurant.Location), 2) : null;

        return new RestaurantSummary(
            restaurant.Id,
            restaurant.Name,
            restaurant.Cuisine,
            restaurant.Address,
            restaurant.Description,
            restaurant.Latitude,
            restaurant.Longitude,
            isOpen,
            nextOpening,
            restaurant.AveragePrepMinutes,
            distance);
    }

    public static MenuDto ToMenu(this Restaurant restaurant, bool includeUnavailable) => new(
        restaurant.Id,
        restaurant.Currency,
        restaurant.MenuItems
            .Where(i => includeUnavailable || i.IsAvailable)
            .GroupBy(i => i.Category)
            .OrderBy(g => g.Min(i => i.SortOrder == 0 ? int.MaxValue : i.SortOrder))
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new MenuCategoryDto(
                g.Key,
                g.OrderBy(i => i.SortOrder).ThenBy(i => i.Name, StringComparer.Ordinal)
                    .Select(i => new MenuItemDto(i.Id, i.Name, i.Description, i.Price, i.IsAvailable, i.IsVegetarian, i.IsSpicy))
                    .ToArray()))
            .ToArray());
}
