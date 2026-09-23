using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;
using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.Negotiations;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Pricing.Infrastructure.Persistence;

public static class Schema
{
    public const string Name = "pricing";
}

public sealed class PricingDbContext(DbContextOptions<PricingDbContext> options) : ModuleDbContext<PricingDbContext>(options), IRepositoryWritePolicy
{
    private readonly Dictionary<Guid, bool> _contractLocks = [];
    protected override bool RequireTransaction => true;
    protected override bool AllowAggregateDeletion => false;
    protected override bool RequireMaterializedAggregates => true;
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<SkuPrice> SkuPrices => Set<SkuPrice>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<NegotiatedPrice> NegotiatedPrices => Set<NegotiatedPrice>();
    public DbSet<PricePeriodRow> PricePeriods => Set<PricePeriodRow>();
    public DbSet<PriceTierRow> PriceTiers => Set<PriceTierRow>();
    public DbSet<PriceChangeRow> PriceChanges => Set<PriceChangeRow>();
    public DbSet<ContractPriceAmendmentRow> ContractPriceAmendments => Set<ContractPriceAmendmentRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => PricingModel.Configure(modelBuilder);

    internal void RecordContractLock(Guid id, bool exclusive)
    {
        _contractLocks[id] = exclusive || _contractLocks.GetValueOrDefault(id);
    }

    internal void ClearContractLocks() => _contractLocks.Clear();

    public void EnsureDeleteAllowed(Type entityType) => throw new InvalidOperationException("Pricing records must change through aggregate behavior; deletion is forbidden.");
    public void EnsureBulkWriteAllowed(Type entityType) => throw new InvalidOperationException("Bulk writes bypass Pricing invariants and concurrency checks.");

    protected override async Task HydrateAggregateAsync(AggregateRoot aggregate, CancellationToken cancellationToken)
    {
        if (aggregate is not SkuPrice price)
        {
            return;
        }
        var tracked = Entry(price).State != EntityState.Detached;
        if (tracked && price.ContractId is Guid contractId && !_contractLocks.ContainsKey(contractId))
        {
            throw new InvalidOperationException("Lock the contract before loading a tracked SKU price.");
        }
        IQueryable<T> Rows<T>()
            where T : class => tracked ? Set<T>() : Set<T>().AsNoTracking();
        var periods = await Rows<PricePeriodRow>().Where(x => x.SkuPriceId == price.Id).OrderBy(x => x.EffectiveFrom).ToListAsync(cancellationToken);
        var tiers = await Rows<PriceTierRow>().Where(x => PricePeriods.Any(period => period.Id == x.PricePeriodId && period.SkuPriceId == price.Id)).OrderBy(x => x.MinimumQuantity).ToListAsync(cancellationToken);
        var changes = await Rows<PriceChangeRow>().Where(x => x.SkuPriceId == price.Id).OrderBy(x => x.Revision).ToListAsync(cancellationToken);
        var amendments = await Rows<ContractPriceAmendmentRow>().Where(x => x.SkuPriceId == price.Id).OrderBy(x => x.ProposedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var currentStamp = await SkuPrices.AsNoTracking().Where(x => x.Id == price.Id).Select(x => x.ConcurrencyStamp).SingleAsync(cancellationToken);
        if (currentStamp != price.ConcurrencyStamp)
        {
            throw new DomainException("Pricing changed while its timeline was being loaded. Reload and retry.", "PricingConcurrencyConflict");
        }
        var byPeriod = tiers.ToLookup(x => x.PricePeriodId);
        price.RestoreChildren(periods.Select(row => new PricePeriod(row.Id, EffectivePeriod.Create(row.EffectiveFrom, row.EffectiveTo),
                byPeriod[row.Id].Select(tier => PriceTier.Create(tier.MinimumQuantity, tier.Amount)), row.ContractPriceAmendmentId)),
            changes.Select(row => new PriceChange(row.Kind, row.ActorId, row.OccurredAt, PricingJson.DeserializePeriods(row.BeforeJson), PricingJson.DeserializePeriods(row.AfterJson), row.ContractPriceAmendmentId)),
            amendments.Select(PricingJson.DeserializeAmendment));
    }

    protected override Task PrepareAggregatesAsync(IReadOnlyList<AggregateRoot> aggregates, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries().Any(entry => entry.Entity is PricePeriodRow or PriceTierRow or PriceChangeRow or ContractPriceAmendmentRow && entry.State is EntityState.Modified or EntityState.Deleted or EntityState.Added))
        {
            throw new InvalidOperationException("Persist Pricing children through their tracked SkuPrice aggregate.");
        }
        foreach (var aggregate in aggregates)
        {
            var state = Entry(aggregate).State;
            if (state is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }
            if (aggregate is Contract contract && state == EntityState.Modified && !_contractLocks.GetValueOrDefault(contract.Id))
            {
                throw new InvalidOperationException("Contract transitions require an exclusive contract lock.");
            }
            if (aggregate is SkuPrice price)
            {
                if (price.ContractId is Guid contractId && !_contractLocks.ContainsKey(contractId) &&
                    !ChangeTracker.Entries<Contract>().Any(entry => entry.Entity.Id == contractId && entry.State == EntityState.Added))
                {
                    throw new InvalidOperationException("Contract prices require a contract lock before loading the SKU price.");
                }
                SyncPrice(price);
            }
        }
        return Task.CompletedTask;
    }

    private void SyncPrice(SkuPrice price)
    {
        var periodIds = PricePeriods.Local.Where(x => x.SkuPriceId == price.Id).Select(x => x.Id).Union(price.Periods.Select(x => x.Id)).ToHashSet();
        Sync(PricePeriods.Local.Where(x => x.SkuPriceId == price.Id).ToArray(), price.Periods.Select(period => new PricePeriodRow
        {
            Id = period.Id,
            SkuPriceId = price.Id,
            EffectiveFrom = period.EffectivePeriod.From,
            EffectiveTo = period.EffectivePeriod.To,
            ContractPriceAmendmentId = period.ContractPriceAmendmentId
        }), x => x.Id);
        Sync(PriceTiers.Local.Where(x => periodIds.Contains(x.PricePeriodId)).ToArray(), price.Periods.SelectMany(period => period.Tiers.Select(tier => new PriceTierRow
        {
            PricePeriodId = period.Id,
            MinimumQuantity = tier.MinimumQuantity,
            Amount = tier.Amount
        })), x => (x.PricePeriodId, x.MinimumQuantity));

        var storedChanges = PriceChanges.Local.Where(x => x.SkuPriceId == price.Id).ToDictionary(x => x.Revision);
        for (var index = 0; index < price.Changes.Count; index++)
        {
            var change = price.Changes[index];
            var revision = index + 1L;
            var row = new PriceChangeRow
            {
                SkuPriceId = price.Id,
                Revision = revision,
                Kind = change.Kind,
                ActorId = change.ActorId,
                OccurredAt = change.OccurredAt,
                BeforeJson = PricingJson.SerializePeriods(change.Before),
                AfterJson = PricingJson.SerializePeriods(change.After),
                ContractPriceAmendmentId = change.ContractPriceAmendmentId
            };
            if (storedChanges.Remove(revision, out var stored))
            {
                if (stored.Kind != row.Kind || stored.ActorId != row.ActorId || stored.OccurredAt != row.OccurredAt || stored.ContractPriceAmendmentId != row.ContractPriceAmendmentId || !SameJson(stored.BeforeJson, row.BeforeJson) || !SameJson(stored.AfterJson, row.AfterJson))
                {
                    throw new InvalidOperationException("Persisted price audit is immutable.");
                }
            }
            else
            {
                PriceChanges.Add(row);
            }
        }
        if (storedChanges.Count != 0 || price.Revision != price.Changes.Count)
        {
            throw new InvalidOperationException("Price revision and immutable audit must remain consistent.");
        }

        var storedAmendments = ContractPriceAmendments.Local.Where(x => x.SkuPriceId == price.Id).ToDictionary(x => x.Id);
        foreach (var amendment in price.ContractPriceAmendments)
        {
            var proposal = PricingJson.SerializeProposal(amendment);
            if (storedAmendments.Remove(amendment.Id, out var row))
            {
                if (!SameJson(row.ProposalJson, proposal) || row.ContractId != amendment.ContractId || row.ProposedAt != amendment.ProposedAt ||
                    row.Status != ContractPriceAmendmentStatus.PendingApproval && (row.Status != amendment.Status || row.DecidedBy != amendment.DecidedBy || row.DecidedAt != amendment.DecidedAt))
                {
                    throw new InvalidOperationException("Persisted amendment proposals and decisions are immutable.");
                }
                row.Status = amendment.Status;
                row.DecidedBy = amendment.DecidedBy;
                row.DecidedAt = amendment.DecidedAt;
            }
            else
            {
                ContractPriceAmendments.Add(new ContractPriceAmendmentRow
                {
                    Id = amendment.Id,
                    SkuPriceId = price.Id,
                    ContractId = amendment.ContractId,
                    ProposalJson = proposal,
                    ProposedAt = amendment.ProposedAt,
                    Status = amendment.Status,
                    DecidedBy = amendment.DecidedBy,
                    DecidedAt = amendment.DecidedAt
                });
            }
        }
        if (storedAmendments.Count != 0)
        {
            throw new InvalidOperationException("Persisted amendments cannot be deleted.");
        }
    }

    private static bool SameJson(string first, string second) => JsonNode.DeepEquals(JsonNode.Parse(first), JsonNode.Parse(second));

    private void Sync<T, TKey>(IEnumerable<T> current, IEnumerable<T> desired, Func<T, TKey> key)
        where T : class
        where TKey : notnull
    {
        var previous = current.ToDictionary(key);
        foreach (var row in desired)
        {
            if (previous.Remove(key(row), out var existing))
            {
                Entry(existing).CurrentValues.SetValues(row);
            }
            else
            {
                Set<T>().Add(row);
            }
        }
        foreach (var row in previous.Values)
        {
            Set<T>().Remove(row);
        }
    }
}