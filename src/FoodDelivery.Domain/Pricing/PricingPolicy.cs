namespace FoodDelivery.Domain.Pricing;

/// <summary>
/// Fee rules applied on top of the item subtotal. All amounts are in the restaurant's currency.
/// </summary>
public sealed record PricingPolicy
{
    /// <summary>Flat delivery fee charged when the subtotal is below <see cref="FreeDeliveryThreshold"/>.</summary>
    public decimal DeliveryFee { get; init; } = 2.49m;

    /// <summary>Subtotal at or above which delivery is free. <c>null</c> disables free delivery.</summary>
    public decimal? FreeDeliveryThreshold { get; init; } = 30.00m;

    /// <summary>Orders below this subtotal pay the difference as a small-order surcharge.</summary>
    public decimal MinimumOrderSubtotal { get; init; } = 10.00m;

    /// <summary>Service fee as a fraction of the subtotal (0.05 = 5%).</summary>
    public decimal ServiceFeeRate { get; init; } = 0.05m;

    /// <summary>Upper bound for the service fee.</summary>
    public decimal ServiceFeeCap { get; init; } = 3.00m;

    public static PricingPolicy Default { get; } = new();
}
