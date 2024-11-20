namespace FoodDelivery.Restaurants.Api.Endpoints;

internal static class CachePolicies
{
    public const string Menu = "menu";
    public const string Listing = "listing";

    public const string AllMenusTag = "menus";
    public const string ListingTag = "restaurants";

    public static string MenuTag(Guid restaurantId) => $"menu:{restaurantId:N}";
}
