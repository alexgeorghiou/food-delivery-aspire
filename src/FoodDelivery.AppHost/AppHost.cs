var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure ---------------------------------------------------------------------------

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("food-delivery-postgres")
    .WithLifetime(ContainerLifetime.Persistent);

var restaurantsDb = postgres.AddDatabase("restaurantsdb");
var ordersDb = postgres.AddDatabase("ordersdb");

var cache = builder.AddRedis("cache")
    .WithLifetime(ContainerLifetime.Persistent);

var messaging = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

// Services ---------------------------------------------------------------------------------

var restaurantsApi = builder.AddProject<Projects.FoodDelivery_Restaurants_Api>("restaurants-api")
    .WithReference(restaurantsDb)
    .WithReference(cache)
    .WaitFor(restaurantsDb)
    .WaitFor(cache)
    .WithHttpHealthCheck("/health");

var ordersApi = builder.AddProject<Projects.FoodDelivery_Orders_Api>("orders-api")
    .WithReference(ordersDb)
    .WithReference(messaging)
    .WithReference(restaurantsApi)
    .WaitFor(ordersDb)
    .WaitFor(messaging)
    .WaitFor(restaurantsApi)
    .WithHttpHealthCheck("/health");

var dispatchWorker = builder.AddProject<Projects.FoodDelivery_Dispatch_Worker>("dispatch-worker")
    .WithReference(cache)
    .WithReference(messaging)
    .WaitFor(cache)
    .WaitFor(messaging)
    .WithHttpHealthCheck("/health");

// Frontend ---------------------------------------------------------------------------------

// The Vite dev server proxies /api/* to the services, so the browser only talks to one origin.
builder.AddViteApp("web", "../../web")
    .WithReference(restaurantsApi)
    .WithReference(ordersApi)
    .WithReference(dispatchWorker)
    .WithEnvironment("RESTAURANTS_API_URL", restaurantsApi.GetEndpoint("http"))
    .WithEnvironment("ORDERS_API_URL", ordersApi.GetEndpoint("http"))
    .WithEnvironment("DISPATCH_API_URL", dispatchWorker.GetEndpoint("http"))
    .WaitFor(restaurantsApi)
    .WaitFor(ordersApi)
    .WithExternalHttpEndpoints();

builder.Build().Run();
