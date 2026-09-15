using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Inventory.Domain.Stocks;

public sealed class InventoryStock : AggregateRoot
{
    private InventoryStock() { }

    public InventoryStock(Guid warehouseId, Guid productItemId)
    {
        WarehouseId = Guard.Id(warehouseId);
        ProductItemId = Guard.Id(productItemId);
    }

    public Guid WarehouseId { get; private set; }
    public Guid ProductItemId { get; private set; }
    public int OnHand { get; private set; }
    public int Reserved { get; private set; }
    public int Available => OnHand - Reserved;

    public void Receive(int quantity)
    {
        RequirePositive(quantity);
        OnHand = Add(OnHand, quantity);
    }

    public void Reserve(int quantity)
    {
        RequirePositive(quantity);
        if (quantity > Available)
        {
            throw InvalidQuantity("Insufficient available stock.");
        }
        Reserved += quantity;
    }

    public void Release(int quantity)
    {
        RequirePositive(quantity);
        if (quantity > Reserved)
        {
            throw InvalidQuantity("Cannot release more than the reserved quantity.");
        }
        Reserved -= quantity;
    }

    public void IssueReserved(int quantity)
    {
        RequirePositive(quantity);
        if (quantity > Reserved)
        {
            throw InvalidQuantity("Cannot issue more than the reserved quantity.");
        }
        Reserved -= quantity;
        OnHand -= quantity;
    }

    public void Adjust(int quantityDelta)
    {
        if (quantityDelta == 0)
        {
            throw InvalidQuantity("Adjustment quantity cannot be zero.");
        }
        var adjusted = Add(OnHand, quantityDelta);
        if (adjusted < Reserved)
        {
            throw InvalidQuantity("Adjustment cannot reduce stock below its reserved quantity.");
        }
        OnHand = adjusted;
    }

    public override void MarkDeleted(string? actorId, DateTimeOffset utcNow)
    {
        throw new DomainException("Inventory stock cannot be deleted.");
    }

    public override void Restore()
    {
        throw new DomainException("Inventory stock cannot be restored.");
    }

    private static void RequirePositive(int quantity)
    {
        if (quantity <= 0)
        {
            throw InvalidQuantity("Quantity must be positive.");
        }
    }

    private static int Add(int left, int right)
    {
        try
        {
            return checked(left + right);
        }
        catch (OverflowException exception)
        {
            throw new DomainException("Quantity exceeds the supported range.", exception, "InvalidQuantity");
        }
    }

    private static DomainException InvalidQuantity(string message) => new(message, "InvalidQuantity");
}
