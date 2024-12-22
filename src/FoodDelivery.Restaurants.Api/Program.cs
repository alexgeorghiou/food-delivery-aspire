using System.Text.Json.Serialization;
using FoodDelivery.Restaurants.Api.Data;
using FoodDelivery.Restaurants.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<RestaurantsDbContext>("restaurantsdb");
builder.AddRedisOutputCache("cache");

builder.Services.AddOutputCache(options =>
{
    options.AddPolicy(CachePolicies.Menu, policy => policy
        .Expire(TimeSpan.FromMinutes(10))
        .SetVaryByQuery("includeUnavailable")
        .Tag(CachePolicies.AllMenusTag)
        .AddPolicy<MenuTagPolicy>());

    // Listing includes "open now", so it is cached briefly rather than evicted on the clock.
    options.AddPolicy(CachePolicies.Listing, policy => policy
        .Expire(TimeSpan.FromSeconds(30))
        .SetVaryByQuery("cuisine", "lat", "lon", "openNow")
        .Tag(CachePolicies.ListingTag));
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseOutputCache();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();
app.MapRestaurantEndpoints();
app.MapMenuEndpoints();

await app.InitializeDatabaseAsync();
await app.RunAsync();
