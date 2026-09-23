using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Integrations.Catalog;

public interface ICatalogSkuClient
{
    Task<ApiResult<bool>> ExistsAsync(Guid skuId, CancellationToken cancellationToken);
}