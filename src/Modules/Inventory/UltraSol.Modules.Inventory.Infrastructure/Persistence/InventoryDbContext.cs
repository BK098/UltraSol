using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using UltraSol.Modules.Inventory.Domain.Adjustments;
using UltraSol.Modules.Inventory.Domain.Reservations;
using UltraSol.Modules.Inventory.Domain.Stocks;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Infrastructure.Messaging;
using UltraSol.Shared.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Persistence.Extensions;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Inventory.Infrastructure.Persistence;

public static class Schema
{
    public const string Name = "inventory";
}

public sealed class InventoryDbContext : ModuleDbContext<InventoryDbContext>, IRepositoryWritePolicy
{
    private readonly TimeProvider _timeProvider;

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options, TimeProvider? timeProvider = null) : base(options)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        ChangeTracker.DeleteOrphansTiming = CascadeTiming.OnSaveChanges;
    }

    protected override bool RequireTransaction => true;
    protected override bool AllowAggregateDeletion => false;
    protected override bool RequireMaterializedAggregates => true;

    public DbSet<InventoryStock> Stocks => Set<InventoryStock>();
    public DbSet<StockReservation> Reservations => Set<StockReservation>();
    public DbSet<InventoryAdjustment> Adjustments => Set<InventoryAdjustment>();
    public DbSet<StockMovement> Movements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema.Name);
        InventoryModel.Configure(modelBuilder);
        modelBuilder.MapMailbox();
        ApplySnakeCase(modelBuilder);
    }

    protected override async Task HydrateAggregateAsync(AggregateRoot aggregate, CancellationToken cancellationToken)
    {
        switch (aggregate)
        {
            case StockReservation reservation:
                await Entry(reservation).Collection(nameof(StockReservation.Lines)).LoadAsync(cancellationToken);
                break;
            case InventoryAdjustment adjustment:
                await Entry(adjustment).Collection(nameof(InventoryAdjustment.Lines)).LoadAsync(cancellationToken);
                break;
        }
    }

    protected override Task PrepareAggregatesAsync(IReadOnlyList<AggregateRoot> aggregates, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ChangeTracker.DetectChanges();
        RejectImmutableRows();

        var dirtyRoots = new HashSet<AggregateRoot>(ReferenceEqualityComparer.Instance);
        MarkRemovedAdjustmentLines(aggregates);
        ValidateReservationLines(aggregates, dirtyRoots);
        ValidateAdjustmentLines(aggregates, dirtyRoots);
        var now = _timeProvider.GetUtcNow();

        foreach (var root in aggregates)
        {
            if (root.IsDeleted)
            {
                throw new InvalidOperationException("Inventory aggregates cannot be soft deleted.");
            }

            var entry = Entry(root);
            if (entry.State == EntityState.Added)
            {
                root.MarkCreated(root.CreatedBy, now);
                continue;
            }

            if (entry.State != EntityState.Modified && !dirtyRoots.Contains(root))
            {
                continue;
            }

            var originalStamp = entry.Property(nameof(AggregateRoot.ConcurrencyStamp)).OriginalValue as string;
            if (root.ConcurrencyStamp == originalStamp)
            {
                root.MarkUpdated(root.UpdatedBy, now);
            }
        }

        ChangeTracker.DetectChanges();
        return Task.CompletedTask;
    }

    public void EnsureDeleteAllowed(Type entityType) =>
        throw new InvalidOperationException("Inventory records cannot be deleted through repositories.");

    public void EnsureBulkWriteAllowed(Type entityType) =>
        throw new InvalidOperationException("Inventory writes require tracked aggregates and concurrency checks.");

    private void RejectImmutableRows()
    {
        foreach (var entry in ChangeTracker.Entries<StockMovement>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("Stock movements are immutable.");
            }
        }

        foreach (var entry in ChangeTracker.Entries().Where(entry => entry.Metadata.Name == "InventoryWarehouse"))
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("The inventory warehouse seed is immutable.");
            }
        }
    }

    private void ValidateReservationLines(IReadOnlyList<AggregateRoot> aggregates, HashSet<AggregateRoot> dirtyRoots)
    {
        foreach (var line in ChangeTracker.Entries<ReservationLine>().Where(IsDirty))
        {
            var owner = FindOwner<StockReservation>(aggregates, OwnerId(line, "ReservationId"));
            var ownerEntry = Entry(owner);
            if (ownerEntry.State != EntityState.Added && ownerEntry.Property(value => value.Status).OriginalValue != ReservationStatus.Active)
            {
                throw new InvalidOperationException("Terminal reservation lines are immutable.");
            }
            dirtyRoots.Add(owner);
        }
    }

    private void ValidateAdjustmentLines(IReadOnlyList<AggregateRoot> aggregates, HashSet<AggregateRoot> dirtyRoots)
    {
        foreach (var line in ChangeTracker.Entries<AdjustmentLine>().Where(IsDirty))
        {
            var owner = FindOwner<InventoryAdjustment>(aggregates, OwnerId(line, "AdjustmentId"));
            var ownerEntry = Entry(owner);
            if (ownerEntry.State != EntityState.Added && ownerEntry.Property(value => value.Status).OriginalValue != AdjustmentStatus.Draft)
            {
                throw new InvalidOperationException("Posted or cancelled adjustment lines are immutable.");
            }
            dirtyRoots.Add(owner);
        }
    }

    private void MarkRemovedAdjustmentLines(IReadOnlyList<AggregateRoot> aggregates)
    {
        foreach (var line in ChangeTracker.Entries<AdjustmentLine>().Where(entry => entry.State is not (EntityState.Added or EntityState.Deleted)))
        {
            var ownerId = line.Property<Guid>("AdjustmentId").OriginalValue;
            var owner = aggregates.OfType<InventoryAdjustment>().SingleOrDefault(value => value.Id == ownerId);
            if (owner is null || owner.Lines.Contains(line.Entity))
            {
                continue;
            }
            if (!IsMaterialized(owner))
            {
                throw new InvalidOperationException("Load the complete Inventory aggregate before changing its lines.");
            }
            if (Entry(owner).Property(value => value.Status).OriginalValue != AdjustmentStatus.Draft)
            {
                throw new InvalidOperationException("Posted or cancelled adjustment lines are immutable.");
            }
            line.State = EntityState.Deleted;
        }
    }

    private TAggregate FindOwner<TAggregate>(IReadOnlyList<AggregateRoot> aggregates, Guid ownerId)
        where TAggregate : AggregateRoot
    {
        var owner = aggregates.OfType<TAggregate>().SingleOrDefault(value => value.Id == ownerId);
        if (owner is null || !IsMaterialized(owner))
        {
            throw new InvalidOperationException("Load the complete Inventory aggregate before changing its lines.");
        }
        return owner;
    }

    private static Guid OwnerId<TEntity>(EntityEntry<TEntity> entry, string propertyName)
        where TEntity : class
    {
        var property = entry.Property<Guid>(propertyName);
        return property.CurrentValue != Guid.Empty ? property.CurrentValue : property.OriginalValue;
    }

    private static bool IsDirty(EntityEntry entry) => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;

    private static void ApplySnakeCase(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ModelConfigure.Snake(property.Name));
            }
        }
    }
}
