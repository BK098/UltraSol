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
}