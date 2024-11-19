namespace FoodDelivery.Restaurants.Api.Endpoints;

public sealed record RestaurantSummary(
    Guid Id,
    string Name,
    string Cuisine,
    string Address,
    string Description,
    double Latitude,
    double Longitude,
    bool IsOpen,
    DateTimeOffset? NextOpeningAt,
    int AveragePrepMinutes,
    double? DistanceKm);

public sealed record RestaurantDetails(
    RestaurantSummary Summary,
    string TimeZoneId,
    IReadOnlyList<OpeningPeriodDto> OpeningHours);

public sealed record OpeningPeriodDto(DayOfWeek Day, TimeOnly Opens, TimeOnly Closes);

public sealed record MenuDto(Guid RestaurantId, string Currency, IReadOnlyList<MenuCategoryDto> Categories);

public sealed record MenuCategoryDto(string Name, IReadOnlyList<MenuItemDto> Items);

public sealed record MenuItemDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    bool IsAvailable,
    bool IsVegetarian,
    bool IsSpicy);

public sealed record UpsertMenuItemRequest(
    string Name,
    string Description,
    string Category,
    decimal Price,
    bool IsAvailable = true,
    bool IsVegetarian = false,
    bool IsSpicy = false,
    int SortOrder = 0);

public sealed record ReplaceOpeningHoursRequest(IReadOnlyList<OpeningPeriodDto> Periods);
