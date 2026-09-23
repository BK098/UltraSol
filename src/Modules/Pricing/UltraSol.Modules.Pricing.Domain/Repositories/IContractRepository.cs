using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Shared.Domain.Common.Repositories;

namespace UltraSol.Modules.Pricing.Domain.Repositories;

public interface IContractRepository : IRepository<Contract>
{
    Task<Contract> GetLockedRequiredAsync(Guid id, bool exclusive, CancellationToken cancellationToken = default);
    Task<Contract?> FindByPriceListAsync(Guid priceListId, CancellationToken cancellationToken = default);
}