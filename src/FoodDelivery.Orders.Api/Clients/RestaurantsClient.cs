using System.Net;
using FoodDelivery.Domain.Contracts;

namespace FoodDelivery.Orders.Api.Clients;

/// <summary>
/// Typed client for the Restaurants API. The base address is a service-discovery name
/// ("https+http://restaurants-api") resolved by Aspire; retries, timeouts and circuit
/// breaking come from the standard resilience handler in ServiceDefaults.
/// </summary>
public sealed class RestaurantsClient(HttpClient http)
{
    public async Task<QuoteResponse?> QuoteAsync(Guid restaurantId, QuoteRequest request, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(new Uri($"restaurants/{restaurantId}/quote", UriKind.Relative), request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QuoteResponse>(ct)
            ?? throw new InvalidOperationException("Restaurants API returned an empty quote.");
    }
}
