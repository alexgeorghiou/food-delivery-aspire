namespace FoodDelivery.Restaurants.Api.Data;

/// <summary>
/// Demo catalogue around central Amsterdam. Ids are fixed so the frontend and
/// HTTP request samples stay stable between runs.
/// </summary>
public static class SeedData
{
    public static readonly Guid LuigisId = Guid.Parse("3f1c1a52-6a0e-4c1e-9a4a-0d5b6c7e8f01");
    public static readonly Guid TokyoBowlId = Guid.Parse("3f1c1a52-6a0e-4c1e-9a4a-0d5b6c7e8f02");
    public static readonly Guid TacoTramId = Guid.Parse("3f1c1a52-6a0e-4c1e-9a4a-0d5b6c7e8f03");
    public static readonly Guid GreenLeafId = Guid.Parse("3f1c1a52-6a0e-4c1e-9a4a-0d5b6c7e8f04");
    public static readonly Guid NightOwlId = Guid.Parse("3f1c1a52-6a0e-4c1e-9a4a-0d5b6c7e8f05");

    public static IReadOnlyList<Restaurant> Restaurants()
    {
        var restaurants = Build();

        // Menu order (and therefore category order) follows the order items are listed in below.
        foreach (var restaurant in restaurants)
        {
            for (var i = 0; i < restaurant.MenuItems.Count; i++)
            {
                restaurant.MenuItems[i].SortOrder = i + 1;
            }
        }

        return restaurants;
    }

    private static List<Restaurant> Build() =>
    [
        new Restaurant
        {
            Id = LuigisId,
            Name = "Luigi's Forno",
            Cuisine = "Italian",
            Address = "Prinsengracht 120, Amsterdam",
            Description = "Wood-fired Neapolitan pizza and fresh pasta.",
            Latitude = 52.3745, Longitude = 4.8840,
            AveragePrepMinutes = 18,
            OpeningPeriods = EveryDay(new TimeOnly(11, 30), new TimeOnly(23, 0)),
            MenuItems =
            [
                Item("Margherita", "Pizza", 9.50m, "San Marzano tomato, fior di latte, basil", vegetarian: true),
                Item("Diavola", "Pizza", 12.00m, "Spicy salami, chilli oil, mozzarella", spicy: true),
                Item("Quattro Formaggi", "Pizza", 12.50m, "Four cheeses, honey drizzle", vegetarian: true),
                Item("Tagliatelle al Ragu", "Pasta", 13.50m, "Slow-cooked beef and pork ragu"),
                Item("Cacio e Pepe", "Pasta", 11.00m, "Pecorino, black pepper", vegetarian: true),
                Item("Tiramisu", "Dessert", 6.00m, "Mascarpone, espresso, cocoa", vegetarian: true),
            ],
        },
        new Restaurant
        {
            Id = TokyoBowlId,
            Name = "Tokyo Bowl",
            Cuisine = "Japanese",
            Address = "Utrechtsestraat 45, Amsterdam",
            Description = "Ramen, donburi and gyoza.",
            Latitude = 52.3628, Longitude = 4.8985,
            AveragePrepMinutes = 12,
            OpeningPeriods =
            [
                .. Weekdays(new TimeOnly(12, 0), new TimeOnly(14, 30)),
                .. EveryDay(new TimeOnly(17, 30), new TimeOnly(22, 30)),
            ],
            MenuItems =
            [
                Item("Tonkotsu Ramen", "Ramen", 14.50m, "Pork bone broth, chashu, ajitama"),
                Item("Spicy Miso Ramen", "Ramen", 14.00m, "Miso broth, minced pork, chilli", spicy: true),
                Item("Vegetable Shoyu Ramen", "Ramen", 13.00m, "Soy broth, tofu, greens", vegetarian: true),
                Item("Chicken Katsu Don", "Donburi", 12.50m, "Panko chicken, egg, rice"),
                Item("Pork Gyoza (6)", "Sides", 6.50m, "Pan-fried dumplings"),
                Item("Edamame", "Sides", 4.00m, "Sea salt", vegetarian: true),
            ],
        },
        new Restaurant
        {
            Id = TacoTramId,
            Name = "Taco Tram",
            Cuisine = "Mexican",
            Address = "Javastraat 88, Amsterdam",
            Description = "Street tacos and late-night burritos.",
            Latitude = 52.3632, Longitude = 4.9380,
            AveragePrepMinutes = 10,
            OpeningPeriods =
            [
                .. Days(new TimeOnly(16, 0), new TimeOnly(23, 0), DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday),
                new OpeningPeriodRow { Day = DayOfWeek.Friday, Opens = new TimeOnly(16, 0), Closes = new TimeOnly(3, 0) },
                new OpeningPeriodRow { Day = DayOfWeek.Saturday, Opens = new TimeOnly(14, 0), Closes = new TimeOnly(3, 0) },
                new OpeningPeriodRow { Day = DayOfWeek.Sunday, Opens = new TimeOnly(14, 0), Closes = new TimeOnly(22, 0) },
            ],
            MenuItems =
            [
                Item("Al Pastor Tacos (3)", "Tacos", 10.50m, "Marinated pork, pineapple, salsa verde"),
                Item("Baja Fish Tacos (3)", "Tacos", 11.50m, "Beer-battered fish, chipotle mayo"),
                Item("Mushroom Tacos (3)", "Tacos", 9.50m, "Roasted mushrooms, pickled onion", vegetarian: true),
                Item("Burrito Grande", "Burritos", 12.00m, "Beef, rice, beans, cheese, habanero", spicy: true),
                Item("Nachos", "Sides", 7.00m, "Cheese, jalapeno, guacamole", vegetarian: true),
                Item("Churros", "Dessert", 5.00m, "Cinnamon sugar, chocolate dip", vegetarian: true),
            ],
        },
        new Restaurant
        {
            Id = GreenLeafId,
            Name = "Green Leaf Kitchen",
            Cuisine = "Vegan",
            Address = "Haarlemmerdijk 10, Amsterdam",
            Description = "Plant-based bowls, wraps and smoothies.",
            Latitude = 52.3820, Longitude = 4.8880,
            AveragePrepMinutes = 8,
            OpeningPeriods = EveryDay(new TimeOnly(8, 0), new TimeOnly(20, 0)),
            MenuItems =
            [
                Item("Buddha Bowl", "Bowls", 11.50m, "Quinoa, roasted veg, tahini", vegetarian: true),
                Item("Spicy Tofu Bowl", "Bowls", 12.00m, "Gochujang tofu, rice, kimchi", vegetarian: true, spicy: true),
                Item("Falafel Wrap", "Wraps", 9.00m, "Falafel, hummus, pickles", vegetarian: true),
                Item("Green Smoothie", "Drinks", 5.50m, "Spinach, mango, banana", vegetarian: true),
            ],
        },
        new Restaurant
        {
            Id = NightOwlId,
            Name = "Night Owl Noodles",
            Cuisine = "Chinese",
            Address = "Zeedijk 30, Amsterdam",
            Description = "Hand-pulled noodles, open around the clock.",
            Latitude = 52.3750, Longitude = 4.9005,
            AveragePrepMinutes = 14,
            OpeningPeriods = EveryDay(new TimeOnly(0, 0), new TimeOnly(0, 0)),
            MenuItems =
            [
                Item("Dan Dan Noodles", "Noodles", 11.00m, "Sichuan pepper, minced pork, chilli oil", spicy: true),
                Item("Beef Brisket Noodle Soup", "Noodles", 13.00m, "Slow-braised brisket, clear broth"),
                Item("Garlic Bok Choy", "Sides", 5.50m, "Wok-fried greens", vegetarian: true),
                Item("Pork Bao (2)", "Sides", 6.00m, "Steamed buns, hoisin"),
                Item("Sesame Balls", "Dessert", 4.50m, "Red bean filling", vegetarian: true),
            ],
        },
    ];

    private static List<OpeningPeriodRow> EveryDay(TimeOnly opens, TimeOnly closes) =>
        Enum.GetValues<DayOfWeek>().Select(d => new OpeningPeriodRow { Day = d, Opens = opens, Closes = closes }).ToList();

    private static List<OpeningPeriodRow> Weekdays(TimeOnly opens, TimeOnly closes) =>
        Enum.GetValues<DayOfWeek>()
            .Where(d => d is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            .Select(d => new OpeningPeriodRow { Day = d, Opens = opens, Closes = closes })
            .ToList();

    private static List<OpeningPeriodRow> Days(TimeOnly opens, TimeOnly closes, params DayOfWeek[] days) =>
        days.Select(d => new OpeningPeriodRow { Day = d, Opens = opens, Closes = closes }).ToList();

    private static MenuItem Item(string name, string category, decimal price, string description,
        bool vegetarian = false, bool spicy = false) => new()
    {
        Id = DeterministicId(name),
        Name = name,
        Category = category,
        Price = price,
        Description = description,
        IsVegetarian = vegetarian,
        IsSpicy = spicy,
    };

    private static Guid DeterministicId(string name)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("menu:" + name));
        return new Guid(hash.AsSpan(0, 16));
    }
}
