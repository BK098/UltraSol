using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Shared.Domain.Common.Repositories;

namespace UltraSol.Modules.Pricing.Domain.Repositories;

public interface ISkuPriceRepository : IRepository<SkuPrice>
{
    Task<Guid?> GetContractIdAsync(Guid id, CancellationToken cancellationToken = default);
}