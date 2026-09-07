using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.ValueObjects;
namespace UltraSol.Modules.Catalog.Domain.Catalog.ProductItemAggregate.ValueObjects;

public sealed class OptionSignature : ValueObject
{
    public string Value { get; }
    private OptionSignature(string value) => Value = value;
    public static OptionSignature Create(IEnumerable<OptionSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        var copy = selections.ToArray();
        if (copy.Any(x => x is null) || copy.GroupBy(x => x.VariationId).Any(x => x.Count() > 1))
            throw new DomainException("Selections must contain one option per variation.");
        return new OptionSignature(string.Join("|", copy.OrderBy(x => x.VariationId)
            .Select(x => $"{x.VariationId:N}:{x.OptionId:N}")));
    }
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
    public override string ToString() => Value;
}