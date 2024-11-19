namespace FoodDelivery.Domain.Contracts;

// HTTP contracts shared between the Restaurants API and its callers (Orders API).

public sealed record QuoteRequest(IReadOnlyList<QuoteRequestLine> Lines);

public sealed record QuoteRequestLine(Guid MenuItemId, int Quantity);

public sealed record QuoteResponse(
    Guid RestaurantId,
    string RestaurantName,
    double Latitude,
    double Longitude,
    bool IsOpen,
    string Currency,
    IReadOnlyList<QuoteLine> Lines,
    QuoteTotals Totals,
    IReadOnlyList<string> Problems);

public sealed record QuoteLine(Guid MenuItemId, string Name, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record QuoteTotals(
    decimal Subtotal,
    decimal DeliveryFee,
    decimal SmallOrderSurcharge,
    decimal ServiceFee,
    decimal Total);
