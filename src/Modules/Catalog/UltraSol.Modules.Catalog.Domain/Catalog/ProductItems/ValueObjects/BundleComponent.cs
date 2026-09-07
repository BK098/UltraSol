using UltraSol.Shared.Domain.Common.ValueObjects;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;
namespace UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;

public sealed class BundleComponent : ValueObject
{
    public Guid ProductItemId { get; }
    public int Quantity { get; }
    public BundleComponent(Guid productItemId, int quantity)
    {
        ProductItemId = Guard.Id(productItemId);
        if (quantity <= 0) throw new DomainException("Bundle quantity must be positive.");
        Quantity = quantity;
    }
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ProductItemId;
        yield return Quantity;
    }
}