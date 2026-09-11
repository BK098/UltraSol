using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.ValueObjects;
namespace UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;

public sealed class BundleDefinition : ValueObject
{
    public IReadOnlyList<BundleComponent> Components { get; }
    internal static BundleDefinition FromStorage(BundleComponent[] components) => new(components);
    private BundleDefinition(BundleComponent[] components)
    {
        Components = Array.AsReadOnly(components);
    }
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
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        return Components.Cast<object?>();
    }

    internal BundleDefinition Add(Guid ownerId, ProductItem item, int quantity)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Id == ownerId || item.IsBundle || item.Status == ProductItemStatus.Archived)
        {
            throw new DomainException("Component must be a non-archived regular item other than the bundle itself.");
        }
        if (Components.Any(x => x.ProductItemId == item.Id))
        {
            throw new DomainException("Bundle components must be unique.");
        }
        return new BundleDefinition([.. Components.Append(new BundleComponent(item.Id, quantity)).OrderBy(x => x.ProductItemId)]);
    }

    internal BundleDefinition ChangeQuantity(Guid componentItemId, int quantity)
    {
        EnsureContains(componentItemId);
        var replacement = new BundleComponent(componentItemId, quantity);
        return new BundleDefinition([.. Components.Select(x => x.ProductItemId == componentItemId ? replacement : x)]);
    }

    internal BundleDefinition Remove(Guid componentItemId)
    {
        EnsureContains(componentItemId);
        if (Components.Count == 1)
        {
            throw new DomainException("A bundle requires at least one component.");
        }
        return new BundleDefinition([.. Components.Where(x => x.ProductItemId != componentItemId)]);
    }

    private void EnsureContains(Guid componentItemId)
    {
        if (!Components.Any(x => x.ProductItemId == componentItemId))
        {
            throw new DomainException("Component does not belong to this bundle.");
        }
    }
}