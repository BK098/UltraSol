using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Shared.Domain.Common.Repositories;

namespace UltraSol.Modules.Catalog.Domain.Repositories;

public interface IProductItemRepository : IRepository<ProductItem>
{
    Task<bool> ExistsBySkuAsync(string normalizedSku, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySignatureAsync(Guid productId, string signature, CancellationToken cancellationToken = default);
}