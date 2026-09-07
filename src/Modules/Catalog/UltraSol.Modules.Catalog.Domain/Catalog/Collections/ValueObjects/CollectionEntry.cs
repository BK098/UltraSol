using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;
using UltraSol.Shared.Domain.Common.ValueObjects;

namespace UltraSol.Modules.Catalog.Domain.Catalog.Collections.ValueObjects;

public sealed class CollectionEntry : ValueObject
{
    public Guid ProductId { get; }
    public int SortOrder { get; }

    public CollectionEntry(Guid productId, int sortOrder)
    {
        ProductId = Guard.Id(productId);
        if (sortOrder < 0)
        {
            throw new DomainException("Collection order cannot be negative.");
        }
        SortOrder = sortOrder;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ProductId;
        yield return SortOrder;
    }
}