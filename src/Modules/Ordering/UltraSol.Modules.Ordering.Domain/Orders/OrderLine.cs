using UltraSol.Modules.Ordering.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Entities;

namespace UltraSol.Modules.Ordering.Domain.Orders;

public sealed class OrderLine : BaseEntity
{
    private OrderLine() { }
    internal OrderLine(OrderLineSnapshot snapshot)
    {
        Id = Guid.CreateVersion7();
        ProductItemId = snapshot.ProductItemId;
        ProductId = snapshot.ProductId;
        ProductName = snapshot.ProductName;
        SkuCode = snapshot.SkuCode;
        VariantDescription = snapshot.VariantDescription;
        ImageUrl = snapshot.ImageUrl;
        Quantity = snapshot.Quantity;
        Price = snapshot.Price;
        FinalUnitPrice = Price.FinalUnitPrice;
        LineTotal = checked(FinalUnitPrice * Quantity);
    }
    public Guid ProductItemId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = "";
    public string SkuCode { get; private set; } = "";
    public string VariantDescription { get; private set; } = "";
    public string? ImageUrl { get; private set; }
    public int Quantity { get; private set; }
    public decimal FinalUnitPrice { get; private set; }
    public decimal LineTotal { get; private set; }
    public PriceSnapshot Price { get; private set; } = null!;
    public OrderLineSnapshot Snapshot => new(ProductItemId, ProductId, ProductName, SkuCode, VariantDescription, ImageUrl, Quantity, Price);
}