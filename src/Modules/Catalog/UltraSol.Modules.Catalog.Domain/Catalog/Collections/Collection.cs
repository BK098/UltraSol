using UltraSol.Modules.Catalog.Domain.Catalog.Brands;
using UltraSol.Modules.Catalog.Domain.Catalog.Categories;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections.ValueObjects;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Catalog.Domain.Catalog.Collections;

/// <summary>Aggregate for curated membership or automatic rules, never both.</summary>
public sealed class Collection : AggregateRoot
{
    // Used only for persistence materialization; public creation still enforces business rules.
    private Collection() { Name = null!; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public CollectionStatus Status { get; private set; } = CollectionStatus.Draft;
    private readonly List<CollectionEntry> _entries = [];
    internal void LoadMembership(IEnumerable<CollectionEntry> entries, CollectionRuleSet? rules)
    {
        _entries.Clear();
        _entries.AddRange(entries);
        Rules = rules;
    }
    public CollectionType Type { get; }
    public IReadOnlyList<CollectionEntry> Entries => _entries.AsReadOnly();
    public CollectionRuleSet? Rules { get; private set; }

    private Collection(string name, string? description, CollectionType type, CollectionRuleSet? rules)
    {
        Name = Guard.Required(name, "Collection name");
        Description = description?.Trim();
        Type = type;
        Rules = rules;
    }

    public static Collection CreateManual(string name, string? description = null)
    {
        return new(name, description, CollectionType.Manual, null);
    }

    public static Collection CreateAutomatic(string name, IEnumerable<Brand> brands,
        IEnumerable<Category> categories, RuleMatchMode matchMode = RuleMatchMode.All,
        string? description = null)
    {
        return new(name, description, CollectionType.Automatic, CollectionRuleSet.Create(brands, categories, matchMode));
    }

    public void ReplaceRules(IEnumerable<Brand> brands, IEnumerable<Category> categories,
        RuleMatchMode matchMode = RuleMatchMode.All)
    {
        EnsureType(CollectionType.Automatic);
        var rules = CollectionRuleSet.Create(brands, categories, matchMode);
        Rules = rules;
    }

    public void AddProduct(Product product)
    {
        EnsureType(CollectionType.Manual);
        ArgumentNullException.ThrowIfNull(product);
        product.EnsureNotArchived();
        Guard.Id(product.Id);
        if (_entries.Any(x => x.ProductId == product.Id))
        {
            return;
        }
        _entries.Add(new CollectionEntry(product.Id, _entries.Count));
    }

    public void RemoveProduct(Guid productId)
    {
        EnsureType(CollectionType.Manual);
        Guard.Id(productId);
        if (!_entries.Any(x => x.ProductId == productId))
        {
            return;
        }
        ReplaceEntries(_entries.Where(x => x.ProductId != productId).Select(x => x.ProductId).ToArray());
    }

    public void ReorderProducts(IEnumerable<Guid> productIds)
    {
        EnsureType(CollectionType.Manual);
        ArgumentNullException.ThrowIfNull(productIds);
        var ids = productIds.Select(Guard.Id).ToArray();
        if (ids.Length != _entries.Count || ids.Distinct().Count() != ids.Length ||
            ids.Any(id => !_entries.Any(x => x.ProductId == id)))
        {
            throw new DomainException("Order must contain every collection Product ID exactly once.");
        }
        ReplaceEntries(ids);
    }

    public bool Matches(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (Status != CollectionStatus.Published || product.Status != ProductStatus.Published)
        {
            return false;
        }
        return Type == CollectionType.Manual
            ? _entries.Any(x => x.ProductId == product.Id)
            : Rules!.Matches(product);
    }

    public void Publish()
    {
        if (Status is not (CollectionStatus.Draft or CollectionStatus.Unpublished))
        {
            throw new DomainException("Only Draft or Unpublished collections can be published.");
        }
        Status = CollectionStatus.Published;
    }

    public void Unpublish()
    {
        if (Status != CollectionStatus.Published)
        {
            throw new DomainException("Only Published collections can be unpublished.");
        }
        Status = CollectionStatus.Unpublished;
    }

    public void Archive()
    {
        Status = CollectionStatus.Archived;
    }

    private void ReplaceEntries(Guid[] ids)
    {
        var entries = ids.Select((id, index) => new CollectionEntry(id, index)).ToArray();
        _entries.Clear();
        _entries.AddRange(entries);
    }

    private void EnsureType(CollectionType expected)
    {
        EnsureNotArchived();
        if (Type != expected)
        {
            throw new DomainException("Operation is not valid for this collection type.");
        }
    }

    public void Rename(string name)
    {
        EnsureNotArchived();
        Name = Guard.AgainstNullOrWhiteSpace(name, "Collection name");
    }

    public void ChangeDescription(string? description)
    {
        EnsureNotArchived();
        Description = description?.Trim();
    }

    internal void EnsureNotArchived()
    {
        if (Status == CollectionStatus.Archived)
        {
            throw new DomainException("Archived Collection cannot be modified.");
        }
    }

    public override void MarkDeleted(string? actorId, DateTimeOffset utcNow)
    {
        throw new DomainException("Use Archive instead of soft-delete.");
    }
    public override void Restore()
    {
        throw new DomainException("Archived catalog aggregates cannot be restored.");
    }
}