using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Catalog.Infrastructure.Repositories;

public sealed class ProductItemRepository(CatalogDbContext context) : Repository<ProductItem>(context), IProductItemRepository
{
    public Task<bool> ExistsBySkuAsync(string normalizedSku, CancellationToken cancellationToken = default)
    {
        return context.ProductItems.AnyAsync(x => x.Sku == SKU.Create(normalizedSku), cancellationToken);
    }
    public Task<bool> ExistsBySignatureAsync(Guid productId, string signature, CancellationToken cancellationToken = default)
    {
        return context.ProductItems.AnyAsync(x => x.ProductId == productId && x.OptionSignature == OptionSignature.FromStorage(signature), cancellationToken);
    }
}