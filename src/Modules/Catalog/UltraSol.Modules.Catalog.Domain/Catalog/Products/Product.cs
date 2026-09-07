using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;
using UltraSol.Modules.Catalog.Domain.Catalog.Brands;
using UltraSol.Modules.Catalog.Domain.Catalog.Categories;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Catalog.Domain.Catalog.Products;

/// <summary>Aggregate root for product metadata, variation definitions and media.</summary>
public sealed class Product : AggregateRoot
{
    // Used only for persistence materialization; public creation still enforces business rules.
    private Product() { Name = null!; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public ProductStatus Status { get; private set; } = ProductStatus.Draft;
    public bool IsStructureLocked { get; private set; }
    private readonly List<Variation> _variations = [];
    private readonly List<ProductMedia> _media = [];
    private readonly List<Guid> _categoryIds = [];
    internal void LoadRelated(IEnumerable<Variation> variations, IEnumerable<ProductMedia> media)
    {
        _variations.Clear(); _variations.AddRange(variations);
        _media.Clear(); _media.AddRange(media);
    }
    internal void LoadCategories(IEnumerable<Guid> ids)
    {
        _categoryIds.Clear();
        _categoryIds.AddRange(ids);
    }
    public Guid? BrandId { get; private set; }
    public IReadOnlyList<Guid> CategoryIds => _categoryIds.AsReadOnly();
    public IReadOnlyList<ProductMedia> Media => _media.AsReadOnly();
    public IReadOnlyList<Variation> Variations => _variations.AsReadOnly();

    public void AssignBrand(Brand brand)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(brand);
        brand.EnsureNotArchived();
        BrandId = Guard.Id(brand.Id);
    }

    public void ClearBrand()
    {
        EnsureNotArchived();
        BrandId = null;
    }

    public void AddCategory(Category category)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(category);
        category.EnsureNotArchived();
        var id = Guard.Id(category.Id);
        if (!_categoryIds.Contains(id))
        {
            _categoryIds.Add(id);
        }
    }

    public void RemoveCategory(Guid categoryId)
    {
        EnsureNotArchived();
        _categoryIds.Remove(Guard.Id(categoryId));
    }

    private Product(string name, string? description)
    {
        Name = Guard.Required(name, "Product name");
        Description = description?.Trim();
    }

    public static Product Create(string name, string? description = null)
    {
        return new(name, description);
    }
    public void Rename(string name)
    {
        EnsureNotArchived();
        Name = Guard.Required(name, "Product name");
    }
    public void ChangeDescription(string? description)
    {
        EnsureNotArchived();
        Description = description?.Trim();
    }
    public void Publish()
    {
        if (Status is not (ProductStatus.Draft or ProductStatus.Unpublished))
        {
            throw new DomainException("Product cannot be published from its current status.");
        }
        if (!IsStructureLocked)
        {
            throw new DomainException("Create at least one item before publishing.");
        }
        Status = ProductStatus.Published;
    }
    public void Unpublish()
    {
        if (Status != ProductStatus.Published)
        {
            throw new DomainException("Only published products can be unpublished.");
        }
        Status = ProductStatus.Unpublished;
    }
    public void Archive()
    {
        Status = ProductStatus.Archived;
    }
    public Guid AddVariation(string name)
    {
        EnsureStructureEditable();
        var variation = new Variation(UniqueVariationName(name));
        _variations.Add(variation);
        return variation.Id;
    }
    public void RenameVariation(Guid variationId, string name)
    {
        EnsureStructureEditable();
        var variation = FindVariation(variationId);
        variation.Rename(UniqueVariationName(name, variationId));
    }
    public void RemoveVariation(Guid variationId)
    {
        EnsureStructureEditable();
        _variations.Remove(FindVariation(variationId));
    }
    public Guid AddVariationOption(Guid variationId, string value)
    {
        EnsureNotArchived();
        return FindVariation(variationId).AddOption(value);
    }
    public void RenameVariationOption(Guid variationId, Guid optionId, string value)
    {
        EnsureStructureEditable();
        FindVariation(variationId).RenameOption(optionId, value);
    }
    public void RemoveVariationOption(Guid variationId, Guid optionId)
    {
        EnsureStructureEditable();
        FindVariation(variationId).RemoveOption(optionId);
    }
    public void ValidateSelections(IReadOnlyCollection<OptionSelection> selections)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(selections);
        if (selections.Any(x => x is null) || selections.Count != _variations.Count ||
            selections.Select(x => x.VariationId).Distinct().Count() != selections.Count)
        {
            throw new DomainException("Select exactly one option for every variation.");
        }
        foreach (var selection in selections)
        {
            var variation = FindVariation(selection.VariationId);
            if (!variation.Options.Any(x => x.Id == selection.OptionId))
            {
                throw new DomainException("Option does not belong to the selected variation.");
            }
        }
    }
    // Persist this mutation atomically with the newly created ProductItem.
    internal void RegisterItemCreated()
    {
        EnsureNotArchived();
        IsStructureLocked = true;
    }
    internal void EnsureNotArchived()
    {
        if (Status == ProductStatus.Archived)
        {
            throw new DomainException("Archived products cannot be modified.");
        }
    }
    private void EnsureStructureEditable()
    {
        EnsureNotArchived();
        if (IsStructureLocked)
        {
            throw new DomainException("Variation structure is locked after the first item.");
        }
    }
    private Variation FindVariation(Guid id)
    {
        Guard.Id(id);
        return _variations.SingleOrDefault(x => x.Id == id)
            ?? throw new DomainException("Variation does not belong to product.");
    }
    private string UniqueVariationName(string name, Guid? except = null)
    {
        var normalized = Guard.Required(name, "Variation name");
        if (_variations.Any(x => x.Id != except && string.Equals(x.Name, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException("Variation already exists.");
        }
        return normalized;
    }

    public Guid AddMedia(string url, string? altText = null)
    {
        EnsureNotArchived();
        var media = new ProductMedia(url, altText, _media.Count, _media.Count == 0);
        _media.Add(media);
        return media.Id;
    }
    public void UpdateMedia(Guid mediaId, string url, string? altText = null)
    {
        EnsureNotArchived();
        FindMedia(mediaId).Update(url, altText);
    }
    public void RemoveMedia(Guid mediaId)
    {
        EnsureNotArchived();
        var media = FindMedia(mediaId);
        _media.Remove(media);
        for (var i = 0; i < _media.Count; i++) _media[i].SetOrder(i);
        if (media.IsPrimary && _media.Count > 0) _media[0].SetPrimary(true);
    }
    public void SetPrimaryMedia(Guid mediaId)
    {
        EnsureNotArchived();
        var selected = FindMedia(mediaId);
        foreach (var media in _media)
        {
            media.SetPrimary(media == selected);
        }
    }
    public void ReorderMedia(IEnumerable<Guid> mediaIds)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(mediaIds);
        var ids = mediaIds.ToArray();
        if (ids.Length != _media.Count || ids.Distinct().Count() != ids.Length ||
            ids.Any(id => !_media.Any(x => x.Id == id)))
        {
            throw new DomainException("Order must contain every media ID exactly once.");
        }
        var ordered = ids.Select(FindMedia).ToArray();
        _media.Clear();
        _media.AddRange(ordered);
        for (var i = 0; i < _media.Count; i++)
        {
            _media[i].SetOrder(i);
        }
    }
    private ProductMedia FindMedia(Guid id)
    {
        Guard.Id(id);
        return _media.SingleOrDefault(x => x.Id == id)
            ?? throw new DomainException("Media does not belong to this aggregate.");
    }
    public override void MarkDeleted(string? actorId, DateTimeOffset utcNow)
    {
        throw new DomainException("Catalog aggregates cannot be soft-deleted. Archive the Product.");
    }
    public override void Restore()
    {
        throw new DomainException("Catalog aggregates cannot be restored.");
    }
}