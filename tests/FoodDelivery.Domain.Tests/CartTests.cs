using FoodDelivery.Domain.Pricing;

namespace FoodDelivery.Domain.Tests;

public class CartTests
{
    private static readonly Guid Restaurant = Guid.Parse("8a6f2d1e-0000-4000-8000-000000000001");
    private static readonly Guid Margherita = Guid.Parse("8a6f2d1e-0000-4000-8000-000000000101");
    private static readonly Guid Tiramisu = Guid.Parse("8a6f2d1e-0000-4000-8000-000000000102");

    [Fact]
    public void Empty_cart_totals_are_zero()
    {
        var totals = new Cart(Restaurant).CalculateTotals();

        Assert.Equal(new CartTotals(0, 0, 0, 0, 0, 0), totals);
    }

    [Fact]
    public void Adding_same_item_merges_quantities()
    {
        var cart = new Cart(Restaurant)
            .Add(Margherita, "Margherita", 9.50m)
            .Add(Margherita, "Margherita", 9.50m, 2);

        var line = Assert.Single(cart.Lines);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(28.50m, line.LineTotal);
    }

    [Fact]
    public void Price_change_for_an_existing_line_is_rejected()
    {
        var cart = new Cart(Restaurant).Add(Margherita, "Margherita", 9.50m);
        Assert.Throws<InvalidOperationException>(() => cart.Add(Margherita, "Margherita", 9.90m));
    }

    [Fact]
    public void Removing_an_item_drops_the_line()
    {
        var cart = new Cart(Restaurant)
            .Add(Margherita, "Margherita", 9.50m)
            .Add(Tiramisu, "Tiramisu", 5.00m)
            .Remove(Margherita);

        Assert.Equal(Tiramisu, Assert.Single(cart.Lines).MenuItemId);
    }

    [Fact]
    public void Typical_order_pays_delivery_and_service_fee()
    {
        var cart = new Cart(Restaurant)
            .Add(Margherita, "Margherita", 9.50m, 2)
            .Add(Tiramisu, "Tiramisu", 5.00m);

        var totals = cart.CalculateTotals();

        Assert.Equal(24.00m, totals.Subtotal);
        Assert.Equal(2.49m, totals.DeliveryFee);
        Assert.Equal(0m, totals.SmallOrderSurcharge);
        Assert.Equal(1.20m, totals.ServiceFee);
        Assert.Equal(27.69m, totals.Total);
        Assert.Equal(3, totals.ItemCount);
    }

    [Fact]
    public void Delivery_is_free_at_the_threshold()
    {
        var cart = new Cart(Restaurant).Add(Margherita, "Margherita", 10.00m, 3);

        var totals = cart.CalculateTotals();

        Assert.Equal(30.00m, totals.Subtotal);
        Assert.Equal(0m, totals.DeliveryFee);
    }

    [Fact]
    public void Small_orders_pay_the_difference_to_the_minimum()
    {
        var cart = new Cart(Restaurant).Add(Tiramisu, "Tiramisu", 6.40m);

        var totals = cart.CalculateTotals();

        Assert.Equal(3.60m, totals.SmallOrderSurcharge);
        Assert.Equal(0.32m, totals.ServiceFee);
        Assert.Equal(6.40m + 2.49m + 3.60m + 0.32m, totals.Total);
    }

    [Fact]
    public void Service_fee_is_capped()
    {
        var cart = new Cart(Restaurant).Add(Margherita, "Family tray", 45.00m, 4);

        Assert.Equal(3.00m, cart.CalculateTotals().ServiceFee);
    }

    [Fact]
    public void Service_fee_rounds_half_away_from_zero()
    {
        // 5% of 10.10 = 0.505 -> 0.51
        var cart = new Cart(Restaurant).Add(Margherita, "Margherita", 10.10m);

        Assert.Equal(0.51m, cart.CalculateTotals().ServiceFee);
    }

    [Fact]
    public void Custom_policy_without_free_delivery_always_charges_delivery()
    {
        var policy = PricingPolicy.Default with { FreeDeliveryThreshold = null, DeliveryFee = 3.99m };
        var cart = new Cart(Restaurant).Add(Margherita, "Margherita", 50m, 2);

        Assert.Equal(3.99m, cart.CalculateTotals(policy).DeliveryFee);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(CartLine.MaxQuantityPerLine + 1)]
    public void Quantity_out_of_range_is_rejected(int quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cart(Restaurant).Add(Margherita, "Margherita", 9.50m, quantity));
    }

    [Fact]
    public void Merging_past_the_line_limit_is_rejected()
    {
        var cart = new Cart(Restaurant).Add(Margherita, "Margherita", 1m, CartLine.MaxQuantityPerLine);
        Assert.Throws<ArgumentOutOfRangeException>(() => cart.Add(Margherita, "Margherita", 1m));
    }

    [Fact]
    public void Negative_prices_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cart(Restaurant).Add(Margherita, "Margherita", -0.01m));
    }

    [Fact]
    public void Distinct_item_limit_is_enforced()
    {
        var cart = new Cart(Restaurant);
        for (var i = 0; i < Cart.MaxDistinctItems; i++)
        {
            cart.Add(Guid.NewGuid(), $"Item {i}", 1m);
        }

        Assert.Throws<InvalidOperationException>(() => cart.Add(Guid.NewGuid(), "One too many", 1m));
    }
}
