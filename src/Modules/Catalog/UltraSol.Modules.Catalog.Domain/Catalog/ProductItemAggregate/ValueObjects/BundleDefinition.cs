using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.ValueObjects;
namespace UltraSol.Modules.Catalog.Domain.Catalog.ProductItemAggregate.ValueObjects;

public sealed class BundleDefinition : ValueObject
{
    public IReadOnlyList<BundleComponent> Components { get; }
    private BundleDefinition(BundleComponent[] components) => Components = Array.AsReadOnly(components);
    internal static BundleDefinition Create(Guid ownerId, IEnumerable<(ProductItem Item, int Quantity)> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        var copy = components.ToArray();
        if (copy.Length == 0)
        {
            throw new DomainException("A bundle requires components.");
        }
        if (copy.Any(x => x.Item is null || x.Item.Id == ownerId || x.Item.IsBundle))
        {
            throw new DomainException("A bundle cannot contain itself, null items or other bundles.");
        }
        if (copy.GroupBy(x => x.Item.Id).Any(x => x.Count() > 1))
        {
            throw new DomainException("Bundle components must be unique.");
        }
        return new BundleDefinition([.. copy.Select(x => new BundleComponent(x.Item.Id, x.Quantity)).OrderBy(x => x.ProductItemId)]);
    }
    protected override IEnumerable<object?> GetEqualityComponents() => Components.Cast<object?>();
}