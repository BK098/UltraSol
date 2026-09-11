using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Catalog.Application.Reads;

// Persistence projections only. Each query maps these into its own public response types.
public sealed record NamedData(Guid Id, string Name);
public sealed record MediaData(Guid Id, string Url, string? AltText, int SortOrder, bool IsPrimary);
public sealed record OptionData(Guid Id, string Value);
public sealed record VariationData
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public NamedData Product { get; init; } = null!;
    public int OptionCount { get; init; }
    public IReadOnlyList<OptionData> Options { get; init; } = [];
}
public sealed record SelectionData(Guid VariationId, string VariationName, Guid OptionId, string OptionValue);
public sealed record ComponentData(Guid Id, string Sku, NamedData Product, string Status, int Quantity, string? PrimaryMediaUrl);
public sealed record ProductData(Guid Id, string Name, string? Description, ProductStatus Status, NamedData? Brand,
    string? PrimaryMediaUrl, IReadOnlyList<NamedData> Categories, IReadOnlyList<MediaData> Media, IReadOnlyList<VariationData> Variations);
public sealed record ItemData(Guid Id, string Sku, ProductItemStatus Status, NamedData Product, bool IsBundle,
    string? PrimaryMediaUrl, IReadOnlyList<SelectionData> Selections, IReadOnlyList<MediaData> Media, IReadOnlyList<ComponentData> Components);
public sealed record BrandData
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public string? LogoUrl { get; init; }
    public bool IsActive { get; init; }
    public bool IsArchived { get; init; }
}
public sealed record CategoryData
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public bool IsArchived { get; init; }
    public NamedData? Parent { get; init; }
}
public sealed record CollectionData
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public CollectionType Type { get; init; }
    public CollectionStatus Status { get; init; }
    public RuleMatchMode? MatchMode { get; init; }
    public IReadOnlyList<NamedData> Brands { get; init; } = [];
    public IReadOnlyList<NamedData> Categories { get; init; } = [];
}
public sealed record ProductReadFilter(ProductStatus? Status = null, Guid? BrandId = null, Guid? CategoryId = null, Guid? CollectionId = null);
public sealed record ItemReadFilter(Guid? ProductId = null, ProductItemStatus? Status = null, bool? IsBundle = null);

public interface ICatalogReadStore
{
    Task<PaginatedResult<ProductData>> ProductsAsync(PaginationRequest page, ProductReadFilter filter, CancellationToken ct);
    Task<ProductData> ProductAsync(Guid id, CancellationToken ct);
    Task<PaginatedResult<ItemData>> ItemsAsync(PaginationRequest page, ItemReadFilter filter, CancellationToken ct);
    Task<ItemData> ItemAsync(Guid id, Guid? productId, bool requireBundle, CancellationToken ct);
    Task<PaginatedResult<BrandData>> BrandsAsync(PaginationRequest page, bool? isActive, bool? isArchived, CancellationToken ct);
    Task<BrandData> BrandAsync(Guid id, CancellationToken ct);
    Task<PaginatedResult<CategoryData>> CategoriesAsync(PaginationRequest page, Guid? parentId, bool rootsOnly, bool? isArchived, CancellationToken ct);
    Task<CategoryData> CategoryAsync(Guid id, CancellationToken ct);
    Task<PaginatedResult<CollectionData>> CollectionsAsync(PaginationRequest page, CollectionStatus? status, CollectionType? type, CancellationToken ct);
    Task<CollectionData> CollectionAsync(Guid id, CancellationToken ct);
    Task<PaginatedResult<VariationData>> VariationsAsync(Guid productId, PaginationRequest page, CancellationToken ct);
    Task<VariationData> VariationAsync(Guid productId, Guid variationId, CancellationToken ct);
}