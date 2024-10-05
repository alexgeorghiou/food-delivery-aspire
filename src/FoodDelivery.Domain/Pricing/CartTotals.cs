namespace FoodDelivery.Domain.Pricing;

public sealed record CartTotals(
    decimal Subtotal,
    decimal DeliveryFee,
    decimal SmallOrderSurcharge,
    decimal ServiceFee,
    decimal Total,
    int ItemCount);
