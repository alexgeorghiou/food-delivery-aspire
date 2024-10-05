namespace FoodDelivery.Domain.Pricing;

/// <summary>
/// A basket for a single restaurant. Adding the same menu item twice merges the lines.
/// </summary>
public sealed class Cart
{
    public const int MaxDistinctItems = 30;

    private readonly List<CartLine> _lines = [];

    public Cart(Guid restaurantId)
    {
        if (restaurantId == Guid.Empty)
        {
            throw new ArgumentException("Restaurant id is required.", nameof(restaurantId));
        }

        RestaurantId = restaurantId;
    }

    public Guid RestaurantId { get; }

    public IReadOnlyList<CartLine> Lines => _lines;

    public bool IsEmpty => _lines.Count == 0;

    public Cart Add(Guid menuItemId, string name, decimal unitPrice, int quantity = 1)
    {
        var index = _lines.FindIndex(l => l.MenuItemId == menuItemId);
        if (index >= 0)
        {
            var existing = _lines[index];
            if (existing.UnitPrice != unitPrice)
            {
                throw new InvalidOperationException($"Price for '{name}' changed while it was in the cart.");
            }

            _lines[index] = new CartLine(menuItemId, existing.Name, unitPrice, existing.Quantity + quantity);
            return this;
        }

        if (_lines.Count >= MaxDistinctItems)
        {
            throw new InvalidOperationException($"A cart can hold at most {MaxDistinctItems} distinct items.");
        }

        _lines.Add(new CartLine(menuItemId, name, unitPrice, quantity));
        return this;
    }

    public Cart Remove(Guid menuItemId)
    {
        _lines.RemoveAll(l => l.MenuItemId == menuItemId);
        return this;
    }

    public CartTotals CalculateTotals(PricingPolicy? policy = null)
    {
        policy ??= PricingPolicy.Default;
        return CalculateTotals(_lines, policy);
    }

    public static CartTotals CalculateTotals(IEnumerable<CartLine> lines, PricingPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(policy);

        var materialised = lines as IReadOnlyCollection<CartLine> ?? lines.ToArray();
        if (materialised.Count == 0)
        {
            return new CartTotals(0m, 0m, 0m, 0m, 0m, 0);
        }

        var subtotal = Money.Round(materialised.Sum(l => l.LineTotal));
        var itemCount = materialised.Sum(l => l.Quantity);

        var freeDelivery = policy.FreeDeliveryThreshold is { } threshold && subtotal >= threshold;
        var deliveryFee = freeDelivery ? 0m : Money.Round(policy.DeliveryFee);

        var surcharge = subtotal < policy.MinimumOrderSubtotal
            ? Money.Round(policy.MinimumOrderSubtotal - subtotal)
            : 0m;

        var serviceFee = Money.Round(Math.Min(subtotal * policy.ServiceFeeRate, policy.ServiceFeeCap));

        var total = subtotal + deliveryFee + surcharge + serviceFee;
        return new CartTotals(subtotal, deliveryFee, surcharge, serviceFee, total, itemCount);
    }
}
