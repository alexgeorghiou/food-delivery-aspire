namespace FoodDelivery.Domain.Orders;

public enum OrderStatus
{
    Placed = 0,
    Accepted = 1,
    Preparing = 2,
    ReadyForPickup = 3,
    CourierAssigned = 4,
    PickedUp = 5,
    Delivered = 6,
    Cancelled = 7,
    Rejected = 8,
}

public enum OrderActor
{
    Customer,
    Restaurant,
    Courier,
    System,
}
