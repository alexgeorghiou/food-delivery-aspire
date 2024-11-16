using FoodDelivery.Domain.Geo;
using FoodDelivery.Domain.Menus;

namespace FoodDelivery.Restaurants.Api.Data;

public sealed class Restaurant
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string Cuisine { get; set; }

    public required string Address { get; set; }

    public string Description { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>IANA time zone id, e.g. "Europe/Amsterdam". Opening hours are local to it.</summary>
    public string TimeZoneId { get; set; } = "Europe/Amsterdam";

    public int AveragePrepMinutes { get; set; } = 15;

    public string Currency { get; set; } = "EUR";

    public List<MenuItem> MenuItems { get; set; } = [];

    public List<OpeningPeriodRow> OpeningPeriods { get; set; } = [];

    public GeoPoint Location => new(Latitude, Longitude);

    public OpeningHours ToOpeningHours() =>
        new(OpeningPeriods.Select(p => new OpeningPeriod(p.Day, p.Opens, p.Closes)));

    public TimeZoneInfo ResolveTimeZone() =>
        TimeZoneInfo.TryFindSystemTimeZoneById(TimeZoneId, out var tz) ? tz : TimeZoneInfo.Utc;
}

public sealed class MenuItem
{
    public Guid Id { get; set; }

    public Guid RestaurantId { get; set; }

    public required string Name { get; set; }

    public string Description { get; set; } = string.Empty;

    public required string Category { get; set; }

    public decimal Price { get; set; }

    public bool IsAvailable { get; set; } = true;

    public bool IsVegetarian { get; set; }

    public bool IsSpicy { get; set; }

    public int SortOrder { get; set; }
}

public sealed class OpeningPeriodRow
{
    public int Id { get; set; }

    public Guid RestaurantId { get; set; }

    public DayOfWeek Day { get; set; }

    public TimeOnly Opens { get; set; }

    public TimeOnly Closes { get; set; }
}
