namespace FoodDelivery.Domain.Orders;

public sealed class OrderLine
{
    private OrderLine()
    {
        Name = string.Empty;
    }

    public OrderLine(Guid menuItemId, string name, decimal unitPrice, int quantity)
    {
        Id = Guid.NewGuid();
        MenuItemId = menuItemId;
        Name = name;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public Guid Id { get; private set; }

    public Guid MenuItemId { get; private set; }

    public string Name { get; private set; }

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal => Pricing.Money.Round(UnitPrice * Quantity);
}
