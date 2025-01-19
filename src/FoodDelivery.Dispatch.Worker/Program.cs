using System.Text.Json.Serialization;
using FoodDelivery.Dispatch.Worker.Couriers;
using FoodDelivery.Dispatch.Worker.Dispatching;
using FoodDelivery.Domain.Dispatch;
using FoodDelivery.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRedisClient("cache");
builder.AddRabbitMQClient("messaging");

var dispatchOptions = builder.Configuration.GetSection("Dispatch:Matching").Get<DispatchOptions>() ?? DispatchOptions.Default;
builder.Services.AddSingleton(dispatchOptions);
builder.Services.AddSingleton(new CourierMatcher(dispatchOptions));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ICourierStore, RedisCourierStore>();
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddSingleton<DispatchService>();

builder.Services.AddHostedService<OrderEventsConsumer>();
builder.Services.AddHostedService<PendingDispatchRetrier>();
if (builder.Configuration.GetValue<bool>("Dispatch:SimulateCouriers"))
{
    builder.Services.AddHostedService<CourierSimulator>();
}

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

app.MapDefaultEndpoints();
app.MapCourierEndpoints();

await app.RunAsync();
