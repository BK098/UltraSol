using UltraSol.Shared.Domain.Common.Guards;
using UltraSol.Shared.Domain.Common.ValueObjects;
namespace UltraSol.Modules.Catalog.Domain.Catalog.ProductItemAggregate.ValueObjects;

public sealed class OptionSelection : ValueObject
{
    public Guid VariationId { get; }
    public Guid OptionId { get; }
    public OptionSelection(Guid variationId, Guid optionId)
    {
        VariationId = Guard.Id(variationId);
        OptionId = Guard.Id(optionId);
    }
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return VariationId;
        yield return OptionId;
    }
}