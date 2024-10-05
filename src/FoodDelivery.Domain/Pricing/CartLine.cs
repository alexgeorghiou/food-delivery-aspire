namespace FoodDelivery.Domain.Pricing;

public sealed record CartLine
{
    public const int MaxQuantityPerLine = 50;

    public CartLine(Guid menuItemId, string name, decimal unitPrice, int quantity)
    {
        if (menuItemId == Guid.Empty)
        {
            throw new ArgumentException("Menu item id is required.", nameof(menuItemId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), unitPrice, "Unit price cannot be negative.");
        }

        if (quantity is < 1 or > MaxQuantityPerLine)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, $"Quantity must be between 1 and {MaxQuantityPerLine}.");
        }

        MenuItemId = menuItemId;
        Name = name;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public Guid MenuItemId { get; }

    public string Name { get; }

    public decimal UnitPrice { get; }

    public int Quantity { get; }

    public decimal LineTotal => Money.Round(UnitPrice * Quantity);
}
