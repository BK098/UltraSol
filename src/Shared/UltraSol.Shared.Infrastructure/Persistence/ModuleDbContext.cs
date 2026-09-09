using Microsoft.EntityFrameworkCore;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Shared.Infrastructure.Persistence;

public abstract class ModuleDbContext<TContext>(DbContextOptions<TContext> options) : DbContext(options), IRepositoryMaterializer where TContext : DbContext
{
    private readonly HashSet<object> _materialized = new(ReferenceEqualityComparer.Instance);

    protected virtual bool RequireTransaction => false;
    protected virtual bool AllowAggregateDeletion => true;
    protected virtual bool RequireMaterializedAggregates => false;

    protected void MarkMaterialized(AggregateRoot aggregate) => _materialized.Add(aggregate);
    protected bool IsMaterialized(AggregateRoot aggregate) => _materialized.Contains(aggregate);
    protected virtual Task HydrateAggregateAsync(AggregateRoot aggregate, CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task PrepareAggregatesAsync(IReadOnlyList<AggregateRoot> aggregates, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task MaterializeAsync(object entity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (entity is not AggregateRoot aggregate)
        {
            return;
        }
        var state = Entry(aggregate).State;
        if (state == EntityState.Added)
        {
            MarkMaterialized(aggregate);
            return;
        }
        if (state != EntityState.Detached && IsMaterialized(aggregate))
        {
            return;
        }
        await HydrateAggregateAsync(aggregate, cancellationToken);
        if (Entry(aggregate).State != EntityState.Detached)
        {
            MarkMaterialized(aggregate);
        }
    }

    public sealed override int SaveChanges() => SaveChanges(true);

    public sealed override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Use SaveChangesAsync for module persistence.");

    public sealed override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => SaveChangesAsync(true, cancellationToken);

    public sealed override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (RequireTransaction && Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Module writes require a unit of work or an explicit transaction.");
        }
        if (!acceptAllChangesOnSuccess)
        {
            throw new NotSupportedException("Module persistence requires acceptAllChangesOnSuccess.");
        }
        var aggregates = ChangeTracker.Entries<AggregateRoot>().Select(entry => entry.Entity).ToArray();
        foreach (var aggregate in aggregates)
        {
            var state = Entry(aggregate).State;
            if (state == EntityState.Added)
            {
                MarkMaterialized(aggregate);
            }
            if (!AllowAggregateDeletion && state == EntityState.Deleted)
            {
                throw new InvalidOperationException("Aggregate deletion is not supported by this module.");
            }
            if (RequireMaterializedAggregates && !IsMaterialized(aggregate))
            {
                throw new InvalidOperationException("Load/add complete aggregates through module repositories before saving.");
            }
        }
        await PrepareAggregatesAsync(aggregates, cancellationToken);
        return await base.SaveChangesAsync(true, cancellationToken);
    }
}