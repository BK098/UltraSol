using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;

namespace UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;

/// <summary>Sellable configuration with immutable identity and option selections.</summary>
public sealed class ProductItem : AggregateRoot
{
    private ProductItem()
    {
        Sku = null!;
        OptionSignature = null!;
        OptionSelections = Array.Empty<OptionSelection>();
    }
    internal void LoadComposition(OptionSelection[] selections, BundleDefinition? bundle)
    {
        OptionSelections = Array.AsReadOnly(selections);
        BundleDefinition = bundle;
    }
    private readonly List<ProductItemMedia> _media = [];
    internal void LoadMedia(IEnumerable<ProductItemMedia> media)
    {
        _media.Clear(); _media.AddRange(media);
    }
    public Guid ProductId { get; }
    public SKU Sku { get; private set; }
    public ProductItemStatus Status { get; private set; } = ProductItemStatus.Draft;
    public IReadOnlyList<OptionSelection> OptionSelections { get; private set; }
    public OptionSignature OptionSignature { get; }
    public BundleDefinition? BundleDefinition { get; private set; }
    public bool IsBundle => BundleDefinition is not null;
    public IReadOnlyList<ProductItemMedia> Media => _media.AsReadOnly();

    private ProductItem(Guid id, Guid productId, SKU sku, OptionSelection[] selections, BundleDefinition? bundle) : base(id)
    {
        ProductId = Guard.Id(productId);
        Sku = sku;
        OptionSelections = Array.AsReadOnly(selections);
        OptionSignature = OptionSignature.Create(selections);
        BundleDefinition = bundle;
    }
    public static ProductItem Create(Product product, SKU sku, IEnumerable<OptionSelection> selections)
    {
        return CreateCore(product, sku, selections, null);
    }

    public static ProductItem CreateBundle(Product product, SKU sku, IEnumerable<OptionSelection> selections,
        IEnumerable<(ProductItem Item, int Quantity)> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        return CreateCore(product, sku, selections, components);
    }
    private static ProductItem CreateCore(Product product, SKU sku, IEnumerable<OptionSelection> selections,
        IEnumerable<(ProductItem Item, int Quantity)>? components)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(sku);
        ArgumentNullException.ThrowIfNull(selections);
        var copy = selections.ToArray();
        product.ValidateSelections(copy);
        var id = Guid.NewGuid();
        var bundle = components is null ? null : BundleDefinition.Create(id, components);
        var item = new ProductItem(id, product.Id, sku, copy, bundle);
        product.RegisterItemCreated();
        return item;
    }
    private void EnsureOwnerEditable(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (product.Id != ProductId)
        {
            throw new DomainException("Product does not own this item.");
        }
        product.EnsureNotArchived();
        EnsureNotArchived();
    }

    public void Activate()
    {
        if (Status is not (ProductItemStatus.Draft or ProductItemStatus.Inactive))
        {
            throw new DomainException("Product item cannot be activated from its current status.");
        }
        Status = ProductItemStatus.Active;
    }

    public void Deactivate()
    {
        if (Status != ProductItemStatus.Active)
        {
            throw new DomainException("Only active product items can be deactivated.");
        }
        Status = ProductItemStatus.Inactive;
    }

    public void Archive()
    {
        if (Status == ProductItemStatus.Archived)
        {
            throw new DomainException("Product item is already archived.");
        }
        Status = ProductItemStatus.Archived;
    }

    public void ChangeSku(SKU sku)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(sku);
        Sku = sku;
    }

    private void EnsureNotArchived()
    {
        if (Status == ProductItemStatus.Archived)
        {
            throw new DomainException("Archived product items cannot be modified.");
        }
    }

    public void AddBundleComponent(Product product, ProductItem component, int quantity)
    {
        EnsureOwnerEditable(product);
        BundleDefinition = RequireBundle().Add(Id, component, quantity);
    }

    public void ChangeBundleComponentQuantity(Product product, Guid componentItemId, int quantity)
    {
        EnsureOwnerEditable(product);
        Guard.Id(componentItemId);
        BundleDefinition = RequireBundle().ChangeQuantity(componentItemId, quantity);
    }

    public void RemoveBundleComponent(Product product, Guid componentItemId)
    {
        EnsureOwnerEditable(product);
        Guard.Id(componentItemId);
        BundleDefinition = RequireBundle().Remove(componentItemId);
    }

    private BundleDefinition RequireBundle() => BundleDefinition
        ?? throw new DomainException("Product item is not a bundle.");

    public Guid AddMedia(Product product, string url, string? altText = null)
    {
        EnsureOwnerEditable(product);
        var media = new ProductItemMedia(url, altText, _media.Count, _media.Count == 0);
        _media.Add(media);
        return media.Id;
    }
    public void UpdateMedia(Product product, Guid mediaId, string url, string? altText = null)
    {
        EnsureOwnerEditable(product);
        FindMedia(mediaId).Update(url, altText);
    }
    public void RemoveMedia(Product product, Guid mediaId)
    {
        EnsureOwnerEditable(product);
        var media = FindMedia(mediaId);
        _media.Remove(media);
        for (var i = 0; i < _media.Count; i++)
        {
            _media[i].SetOrder(i);
        }
        if (media.IsPrimary && _media.Count > 0)
        {
            _media[0].SetPrimary(true);
        }
    }
    public void SetPrimaryMedia(Product product, Guid mediaId)
    {
        EnsureOwnerEditable(product);
        var selected = FindMedia(mediaId);
        foreach (var media in _media)
        {
            media.SetPrimary(media == selected);
        }
    }
    public void ReorderMedia(Product product, IEnumerable<Guid> mediaIds)
    {
        EnsureOwnerEditable(product);
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
    private ProductItemMedia FindMedia(Guid id)
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