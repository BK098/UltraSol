using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Domain.Carts;
using UltraSol.Modules.Ordering.Domain.Orders;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Infrastructure.Messaging;
using UltraSol.Shared.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Persistence.Extensions;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Ordering.Infrastructure.Persistence;

public static class Schema
{
    public const string Name = "ordering";
}

public sealed class OrderingDbContext : ModuleDbContext<OrderingDbContext>, IRepositoryWritePolicy
{
    private static readonly HashSet<string> MutableOrderProperties =
    [
        nameof(Order.Status), nameof(Order.OrderVersion), nameof(Order.ConfirmedAt), nameof(Order.CancelledAt),
        nameof(Order.CompletedAt), nameof(Order.UpdatedAt), nameof(Order.UpdatedBy), nameof(Order.ConcurrencyStamp)
    ];

    public OrderingDbContext(DbContextOptions<OrderingDbContext> options) : base(options)
    {
        ChangeTracker.DeleteOrphansTiming = CascadeTiming.OnSaveChanges;
    }

    protected override bool RequireTransaction => true;
    protected override bool AllowAggregateDeletion => false;
    protected override bool RequireMaterializedAggregates => true;

    public DbSet<ShoppingCart> Carts => Set<ShoppingCart>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<CheckoutAttempt> Attempts => Set<CheckoutAttempt>();
    public DbSet<OrderOperation> Operations => Set<OrderOperation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        OrderingModel.Configure(modelBuilder);
        modelBuilder.MapMailbox();
        ApplySnakeCase(modelBuilder);
    }

    protected override async Task HydrateAggregateAsync(AggregateRoot aggregate, CancellationToken cancellationToken)
    {
        if (aggregate is ShoppingCart cart)
        {
            await Entry(cart).Collection(nameof(ShoppingCart.Items)).LoadAsync(cancellationToken);
        }
        else if (aggregate is Order order)
        {
            await Entry(order).Collection(nameof(Order.Lines)).LoadAsync(cancellationToken);
            await Entry(order).Collection(nameof(Order.History)).LoadAsync(cancellationToken);
            await Entry(order).Collection(nameof(Order.Notes)).LoadAsync(cancellationToken);
        }
    }

    protected override Task PrepareAggregatesAsync(IReadOnlyList<AggregateRoot> aggregates, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ChangeTracker.DetectChanges();
        ProtectOrderRows(aggregates);
        RefreshCartStamps(aggregates);
        RefreshProcessVersions();
        ChangeTracker.DetectChanges();
        return Task.CompletedTask;
    }

    public void EnsureDeleteAllowed(Type entityType) =>
        throw new InvalidOperationException("Ordering aggregates cannot be deleted through repositories.");

    public void EnsureBulkWriteAllowed(Type entityType) =>
        throw new InvalidOperationException("Ordering writes require tracked aggregates and concurrency checks.");

    private void ProtectOrderRows(IReadOnlyList<AggregateRoot> aggregates)
    {
        foreach (var entry in ChangeTracker.Entries<Order>())
        {
            if (entry.State == EntityState.Modified && entry.Properties.Any(property => property.IsModified && !MutableOrderProperties.Contains(property.Metadata.Name)))
            {
                throw new InvalidOperationException("Order snapshots are immutable.");
            }
        }

        RejectImmutableChildren<OrderLine>(aggregates, "Order lines are immutable.", allowAddedToExisting: false);
        RejectImmutableChildren<OrderHistory>(aggregates, "Order history is append-only.", allowAddedToExisting: true);
        RejectImmutableChildren<OrderNote>(aggregates, "Order notes are append-only.", allowAddedToExisting: true);
    }

    private void RejectImmutableChildren<TEntity>(IReadOnlyList<AggregateRoot> aggregates, string message, bool allowAddedToExisting)
        where TEntity : class
    {
        foreach (var entry in ChangeTracker.Entries<TEntity>())
        {
            if (entry.State == EntityState.Unchanged)
            {
                continue;
            }
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(message);
            }
            if (entry.State == EntityState.Added && !allowAddedToExisting)
            {
                var orderId = entry.Property<Guid>("OrderId").CurrentValue;
                var owner = aggregates.OfType<Order>().SingleOrDefault(order => order.Id == orderId);
                if (owner is null || Entry(owner).State != EntityState.Added)
                {
                    throw new InvalidOperationException(message);
                }
            }
        }
    }

    private void RefreshCartStamps(IReadOnlyList<AggregateRoot> aggregates)
    {
        foreach (var item in ChangeTracker.Entries<CartItem>().Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var property = item.Property<Guid>("CartId");
            var cartId = property.CurrentValue != Guid.Empty ? property.CurrentValue : property.OriginalValue;
            var cart = aggregates.OfType<ShoppingCart>().SingleOrDefault(value => value.Id == cartId);
            if (cart is null || Entry(cart).State == EntityState.Added)
            {
                continue;
            }
            var stamp = Entry(cart).Property(value => value.ConcurrencyStamp);
            if (stamp.CurrentValue == stamp.OriginalValue)
            {
                cart.RefreshConcurrencyStamp();
            }
        }
    }

    private void RefreshProcessVersions()
    {
        foreach (var entry in ChangeTracker.Entries<CheckoutAttempt>().Where(entry => entry.State == EntityState.Modified))
        {
            RefreshVersion(entry, value => value.Version);
        }
        foreach (var entry in ChangeTracker.Entries<OrderOperation>().Where(entry => entry.State == EntityState.Modified))
        {
            RefreshVersion(entry, value => value.Version);
        }
    }

    private static void RefreshVersion<TEntity>(EntityEntry<TEntity> entry, System.Linq.Expressions.Expression<Func<TEntity, string>> property)
        where TEntity : class
    {
        var version = entry.Property(property);
        if (version.CurrentValue == version.OriginalValue)
        {
            version.CurrentValue = Guid.CreateVersion7().ToString("N");
        }
    }

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