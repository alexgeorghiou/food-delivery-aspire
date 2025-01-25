using FoodDelivery.Domain.Pricing;
using FoodDelivery.Restaurants.Api.Data;
using FoodDelivery.Restaurants.Api.Endpoints;

namespace FoodDelivery.Services.Tests;

public class RestaurantCatalogueTests
{
    [Fact]
    public void Seed_data_has_valid_opening_hours_and_unique_menu_ids()
    {
        var restaurants = SeedData.Restaurants();

        Assert.All(restaurants, r => Assert.NotEmpty(r.ToOpeningHours().Periods));
        var ids = restaurants.SelectMany(r => r.MenuItems).Select(i => i.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void Seed_ids_are_stable_between_calls()
    {
        var first = SeedData.Restaurants().SelectMany(r => r.MenuItems).Select(i => i.Id);
        var second = SeedData.Restaurants().SelectMany(r => r.MenuItems).Select(i => i.Id);
        Assert.Equal(first, second);
    }

    [Fact]
    public void All_night_restaurant_is_always_open()
    {
        var nightOwl = SeedData.Restaurants().Single(r => r.Id == SeedData.NightOwlId);
        var summary = nightOwl.ToSummary(new DateTimeOffset(2026, 3, 4, 3, 30, 0, TimeSpan.Zero));

        Assert.True(summary.IsOpen);
        Assert.Null(summary.NextOpeningAt);
    }

    [Fact]
    public void Closed_restaurant_reports_next_opening()
    {
        var luigis = SeedData.Restaurants().Single(r => r.Id == SeedData.LuigisId);
        // 06:00 UTC is 07:00 in Amsterdam in winter; Luigi's opens 11:30 local.
        var summary = luigis.ToSummary(new DateTimeOffset(2026, 1, 14, 6, 0, 0, TimeSpan.Zero));

        Assert.False(summary.IsOpen);
        Assert.NotNull(summary.NextOpeningAt);
        Assert.True(summary.NextOpeningAt > new DateTimeOffset(2026, 1, 14, 6, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Summary_includes_distance_when_origin_given()
    {
        var luigis = SeedData.Restaurants().Single(r => r.Id == SeedData.LuigisId);
        var summary = luigis.ToSummary(DateTimeOffset.UtcNow, new Domain.Geo.GeoPoint(52.3702, 4.8952));

        Assert.NotNull(summary.DistanceKm);
        Assert.InRange(summary.DistanceKm!.Value, 0.5, 1.5);
    }

    [Fact]
    public void Menu_groups_by_category_and_hides_unavailable_items()
    {
        var luigis = SeedData.Restaurants().Single(r => r.Id == SeedData.LuigisId);
        luigis.MenuItems[0].IsAvailable = false;

        var menu = luigis.ToMenu(includeUnavailable: false);
        var full = luigis.ToMenu(includeUnavailable: true);

        Assert.Equal(["Pizza", "Pasta", "Dessert"], full.Categories.Select(c => c.Name));
        Assert.Equal(full.Categories.Sum(c => c.Items.Count) - 1, menu.Categories.Sum(c => c.Items.Count));
    }

    [Fact]
    public void Seed_menu_prices_are_in_cents()
    {
        Assert.All(SeedData.Restaurants().SelectMany(r => r.MenuItems), i => Assert.Equal(Money.Round(i.Price), i.Price));
    }
}
