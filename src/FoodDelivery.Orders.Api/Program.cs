using System.Text.Json.Serialization;
using FoodDelivery.Messaging;
using FoodDelivery.Orders.Api.Clients;
using FoodDelivery.Orders.Api.Data;
using FoodDelivery.Orders.Api.Endpoints;
using FoodDelivery.Orders.Api.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<OrdersDbContext>("ordersdb");
builder.AddRabbitMQClient("messaging");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddHostedService<CourierAssignedConsumer>();

builder.Services.AddHttpClient<RestaurantsClient>(client =>
    client.BaseAddress = new Uri("https+http://restaurants-api"));

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();
app.MapOrderEndpoints();

await app.InitializeDatabaseAsync();
await app.RunAsync();
