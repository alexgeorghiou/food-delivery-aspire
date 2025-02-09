# Shared multi-stage build for the .NET services.
#   docker build --build-arg PROJECT=FoodDelivery.Orders.Api -t food-delivery/orders-api .
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG PROJECT
WORKDIR /src

# Restore first so the layer is cached until a project file changes.
COPY nuget.config global.json Directory.Build.props Directory.Packages.props ./
COPY src/FoodDelivery.Domain/FoodDelivery.Domain.csproj src/FoodDelivery.Domain/
COPY src/FoodDelivery.Messaging/FoodDelivery.Messaging.csproj src/FoodDelivery.Messaging/
COPY src/FoodDelivery.ServiceDefaults/FoodDelivery.ServiceDefaults.csproj src/FoodDelivery.ServiceDefaults/
COPY src/${PROJECT}/${PROJECT}.csproj src/${PROJECT}/
RUN dotnet restore src/${PROJECT}/${PROJECT}.csproj

COPY src/ src/
RUN dotnet publish src/${PROJECT}/${PROJECT}.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
ARG PROJECT
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_HTTP_PORTS=8080 \
    APP_DLL=${PROJECT}.dll
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]
