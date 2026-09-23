using UltraSol.Shared.Domain.Common.Entities;

namespace UltraSol.Modules.Ordering.Domain.Carts;

public sealed class CartItem : BaseEntity
{
    private CartItem() { }
    internal CartItem(Guid productItemId, int quantity)
    {
        Id = Guid.CreateVersion7();
        ProductItemId = productItemId;
        Quantity = quantity;
    }
    public Guid ProductItemId { get; private set; }
    public int Quantity { get; private set; }
    internal void Change(int quantity) => Quantity = quantity;
}