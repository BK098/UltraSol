using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
namespace UltraSol.Modules.Catalog.Infrastructure.Reads;

public sealed class ClientCatalogReadStore(CatalogDbContext db) : IClientCatalogReadStore
{
    private IQueryable<ClientProduct> Products => db.Products.AsNoTracking().Where(value => value.Status == ProductStatus.Published)
        .Select(value => new ClientProduct(value.Id, value.Name, value.Description, value.Media.Where(media => media.IsPrimary).Select(media => media.Url).FirstOrDefault()));
    public async Task<PaginatedResult<ClientProduct>> ProductsAsync(PaginationRequest page, CancellationToken ct)
    {
        var query = Products;
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            query = query.Where(value => value.Name.ToLower().Contains(page.Search.Trim().ToLower()));
        }
        var count = await query.CountAsync(ct);
        return PaginatedResult<ClientProduct>.Create(await query.OrderBy(value => value.Name).ThenBy(value => value.Id).Skip(page.Skip).Take(page.Take).ToListAsync(ct), count, page);
    }
    public async Task<ClientProductDetail> ProductAsync(Guid id, CancellationToken ct)
    {
        var product = await Products.SingleOrDefaultAsync(value => value.Id == id, ct) ?? throw EntityNotFoundException.For<Product>(id);
        var items = await db.ProductItems.AsNoTracking().Where(value => value.ProductId == id && value.Status == ProductItemStatus.Active &&
            !db.Set<BundleRow>().Any(component => component.BundleItemId == value.Id && db.ProductItems.Any(item => item.Id == component.ComponentItemId &&
                (item.Status != ProductItemStatus.Active || !db.Products.Any(parent => parent.Id == item.ProductId && parent.Status == ProductStatus.Published)))))
            .OrderBy(value => value.Id).Select(value => new ClientItem(value.Id, value.Sku.Value, value.Media.Where(media => media.IsPrimary).Select(media => media.Url).FirstOrDefault())).ToListAsync(ct);
        return new ClientProductDetail(product, items);
    }

    public async Task<IReadOnlyList<CheckoutItemData>> CheckoutItemsAsync(IReadOnlyCollection<Guid> productItemIds, CancellationToken ct)
    {
        var ids = productItemIds.ToArray();
        var items = await db.ProductItems.AsNoTracking().Where(item => ids.Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                item.ProductId,
                SkuCode = item.Sku.Value,
                ItemStatus = item.Status,
                IsBundle = EF.Property<bool>(item, "StoredIsBundle"),
                ItemImage = item.Media.Where(media => media.IsPrimary).Select(media => media.Url).FirstOrDefault(),
                Product = db.Products.Where(product => product.Id == item.ProductId).Select(product => new
                {
                    product.Name,
                    product.Status,
                    Image = product.Media.Where(media => media.IsPrimary).Select(media => media.Url).FirstOrDefault()
                }).Single()
            }).ToListAsync(ct);
        var selections = await db.Set<SelectionRow>().AsNoTracking().Where(row => ids.Contains(row.ProductItemId))
            .Select(row => new
            {
                row.ProductItemId,
                row.VariationId,
                row.OptionId,
                Variation = db.Set<Variation>().Where(variation => variation.Id == row.VariationId).Select(variation => variation.Name).Single(),
                Option = db.Set<VariationOption>().Where(option => option.Id == row.OptionId).Select(option => option.Value).Single()
            }).ToListAsync(ct);
        var components = await db.Set<BundleRow>().AsNoTracking().Where(row => ids.Contains(row.BundleItemId))
            .Select(row => new
            {
                row.BundleItemId,
                row.ComponentItemId,
                row.Quantity,
                ItemStatus = db.ProductItems.Where(item => item.Id == row.ComponentItemId).Select(item => item.Status).Single(),
                ProductStatus = db.ProductItems.Where(item => item.Id == row.ComponentItemId)
                    .Select(item => db.Products.Where(product => product.Id == item.ProductId).Select(product => product.Status).Single()).Single()
            }).ToListAsync(ct);
        var selectionLookup = selections.OrderBy(row => row.Variation).ThenBy(row => row.VariationId).ThenBy(row => row.OptionId)
            .ToLookup(row => row.ProductItemId);
        var componentLookup = components.OrderBy(row => row.ComponentItemId).ToLookup(row => row.BundleItemId);
        return items.Select(item =>
        {
            var bundleComponents = componentLookup[item.Id].ToArray();
            var reasonCode = item.ItemStatus != ProductItemStatus.Active
                ? "ProductItemNotActive"
                : item.Product.Status != ProductStatus.Published
                    ? "ProductNotPublished"
                    : item.IsBundle && bundleComponents.Any(component => component.ItemStatus != ProductItemStatus.Active || component.ProductStatus != ProductStatus.Published)
                        ? "BundleComponentNotSellable"
                        : null;
            return new CheckoutItemData(item.Id, item.ProductId, item.SkuCode, item.Product.Name,
                string.Join(", ", selectionLookup[item.Id].Select(row => $"{row.Variation}: {row.Option}")),
                item.ItemImage ?? item.Product.Image, reasonCode is null, reasonCode, item.IsBundle,
                bundleComponents.Select(component => new CheckoutComponentData(component.ComponentItemId, component.Quantity)).ToArray());
        }).ToArray();
    }
}