using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Modules.Pricing.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Pricing.Infrastructure.Repositories;

public sealed class ContractRepository(PricingDbContext context) : Repository<Contract>(context), IContractRepository
{
    public Task<Contract?> FindByPriceListAsync(Guid priceListId, CancellationToken cancellationToken = default) =>
        FirstOrDefaultAsync(x => x.PriceListId == priceListId, cancellationToken);

    public async Task<Contract> GetLockedRequiredAsync(Guid id, bool exclusive, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Contract locks require an active Pricing transaction.");
        }
        var existing = context.Contracts.Local.SingleOrDefault(x => x.Id == id);
        if (existing is not null && context.Entry(existing).State is EntityState.Added or EntityState.Modified)
        {
            throw new InvalidOperationException("Lock a contract before changing it.");
        }
        // Execute separately from the aggregate query: EF may compose owned validity projections.
        if (exclusive)
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM pricing.contracts WHERE id = {id} FOR UPDATE", cancellationToken);
        }
        else
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM pricing.contracts WHERE id = {id} FOR SHARE", cancellationToken);
        }
        if (existing is not null)
        {
            context.Entry(existing).State = EntityState.Detached;
        }
        var contract = await context.Contracts.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw EntityNotFoundException.For<Contract>(id);
        context.RecordContractLock(id, exclusive);
        await context.MaterializeAsync(contract, cancellationToken);
        return contract;
    }
}