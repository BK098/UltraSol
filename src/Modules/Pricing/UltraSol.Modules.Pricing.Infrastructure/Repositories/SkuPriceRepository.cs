using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Modules.Pricing.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Pricing.Infrastructure.Repositories;

public sealed class SkuPriceRepository(PricingDbContext context) : Repository<SkuPrice>(context), ISkuPriceRepository
{
    public Task<Guid?> GetContractIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.SkuPrices.AsNoTracking().Where(x => x.Id == id).Select(x => x.ContractId).SingleOrDefaultAsync(cancellationToken);
}