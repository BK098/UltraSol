using UltraSol.Shared.Domain.Common.Paging;
namespace UltraSol.Modules.Catalog.Application.Reads;
public sealed record ClientProduct(Guid Id, string Name, string? Description, string? Image);
public sealed record ClientItem(Guid Id, string Sku, string? Image);
public sealed record ClientProductDetail(ClientProduct Product, IReadOnlyList<ClientItem> Items);
public sealed record CheckoutComponentData(Guid ProductItemId, int Quantity);
public sealed record CheckoutItemData(Guid ProductItemId, Guid ProductId, string SkuCode, string ProductName,
    string VariantDescription, string? ImageUrl, bool IsSellable, string? ReasonCode, bool IsBundle,
    IReadOnlyList<CheckoutComponentData> Components);
public interface IClientCatalogReadStore
{
    Task<PaginatedResult<ClientProduct>> ProductsAsync(PaginationRequest page, CancellationToken ct);
    Task<ClientProductDetail> ProductAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<CheckoutItemData>> CheckoutItemsAsync(IReadOnlyCollection<Guid> productItemIds, CancellationToken ct);
}