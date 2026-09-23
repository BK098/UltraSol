using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Payment.Domain.Payments;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Infrastructure.Messaging;
using UltraSol.Shared.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Persistence.Extensions;
using UltraSol.Shared.Infrastructure.Repositories;
using PaymentAggregate = UltraSol.Modules.Payment.Domain.Payments.Payment;

namespace UltraSol.Modules.Payment.Infrastructure.Persistence;

public static class Schema
{
    public const string Name = "payment";
}

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : ModuleDbContext<PaymentDbContext>(options), IRepositoryWritePolicy
{
    protected override bool RequireTransaction => true;
    protected override bool AllowAggregateDeletion => false;
    protected override bool RequireMaterializedAggregates => true;
    public DbSet<PaymentAggregate> Payments => Set<PaymentAggregate>();
    public DbSet<PaymentTransaction> Transactions => Set<PaymentTransaction>();
    public DbSet<PaymentRefund> Refunds => Set<PaymentRefund>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        PaymentModel.Configure(modelBuilder);
        modelBuilder.MapMailbox();
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ModelConfigure.Snake(property.Name));
            }
        }
    }

    protected override async Task HydrateAggregateAsync(AggregateRoot aggregate, CancellationToken cancellationToken)
    {
        if (aggregate is PaymentAggregate payment)
        {
            await Entry(payment).Collection(nameof(PaymentAggregate.Transactions)).LoadAsync(cancellationToken);
            await Entry(payment).Collection(nameof(PaymentAggregate.Refunds)).LoadAsync(cancellationToken);
        }
    }

    protected override Task PrepareAggregatesAsync(IReadOnlyList<AggregateRoot> aggregates, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ChangeTracker.DetectChanges();
        foreach (var payment in ChangeTracker.Entries<PaymentAggregate>())
        {
            if (payment.State == EntityState.Modified && payment.Property(value => value.HasSnapshot).OriginalValue &&
                (payment.Property(value => value.OrderNumber).IsModified || payment.Property(value => value.Amount).IsModified ||
                payment.Property(value => value.Currency).IsModified || payment.Property(value => value.PaymentTerm).IsModified ||
                payment.Property(value => value.NetDays).IsModified || payment.Property(value => value.ReservationExpiresAt).IsModified))
            {
                throw new InvalidOperationException("Confirmed payment money snapshot is immutable.");
            }
        }
        foreach (var entry in ChangeTracker.Entries<PaymentTransaction>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                var owner = aggregates.OfType<PaymentAggregate>().SingleOrDefault(value => value.Id == entry.Entity.PaymentId);
                if (owner is null)
                {
                    throw new InvalidOperationException("Payment transactions require a loaded payment aggregate.");
                }
            }
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified &&
                entry.Properties.Any(property => property.IsModified && property.Metadata.Name is
                    nameof(PaymentTransaction.PaymentId) or nameof(PaymentTransaction.Method) or nameof(PaymentTransaction.Amount) or
                    nameof(PaymentTransaction.Currency) or nameof(PaymentTransaction.IdempotencyKey) or
                    nameof(PaymentTransaction.RequestFingerprint) or nameof(PaymentTransaction.Source) or
                    nameof(PaymentTransaction.Reference) or nameof(PaymentTransaction.ActorId) or nameof(PaymentTransaction.Note) or
                    nameof(PaymentTransaction.CreatedAt) or nameof(PaymentTransaction.ExpiresAt)))
            {
                throw new InvalidOperationException("Payment receipt facts are immutable.");
            }
            if (entry.State == EntityState.Modified && entry.Property(value => value.Status).OriginalValue == PaymentTransactionStatus.Succeeded &&
                (entry.Property(value => value.Status).IsModified || entry.Property(value => value.ProviderReference).IsModified ||
                entry.Property(value => value.ProviderTransactionNo).IsModified || entry.Property(value => value.ReceivedAt).IsModified ||
                entry.Property(value => value.Applied).IsModified))
            {
                throw new InvalidOperationException("Successful payment receipts are immutable.");
            }
        }
        foreach (var entry in ChangeTracker.Entries<PaymentRefund>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                var owner = aggregates.OfType<PaymentAggregate>().SingleOrDefault(value => value.Id == entry.Entity.PaymentId);
                if (owner is null)
                {
                    throw new InvalidOperationException("Payment refunds require a loaded payment aggregate.");
                }
            }
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified &&
                entry.Properties.Any(property => property.IsModified && property.Metadata.Name is
                    nameof(PaymentRefund.PaymentId) or nameof(PaymentRefund.TransactionId) or nameof(PaymentRefund.Amount) or
                    nameof(PaymentRefund.Currency) or nameof(PaymentRefund.Reason) or nameof(PaymentRefund.CreatedAt)))
            {
                throw new InvalidOperationException("Payment refund facts are immutable.");
            }
            if (entry.State == EntityState.Modified &&
                ((entry.Property(value => value.ApprovedBy).OriginalValue is not null &&
                    (entry.Property(value => value.ApprovedBy).IsModified || entry.Property(value => value.ApprovedAt).IsModified)) ||
                (entry.Property(value => value.ReconciledBy).OriginalValue is not null &&
                    (entry.Property(value => value.ReconciledBy).IsModified || entry.Property(value => value.ReconciledAt).IsModified ||
                    entry.Property(value => value.EvidenceReference).IsModified || entry.Property(value => value.State).IsModified))))
            {
                throw new InvalidOperationException("Approved refund audit is immutable.");
            }
        }
        if (ChangeTracker.Entries<PaymentTransaction>().Any(entry => entry.State is EntityState.Added or EntityState.Modified) ||
            ChangeTracker.Entries<PaymentRefund>().Any(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            foreach (var payment in aggregates.OfType<PaymentAggregate>())
            {
                if (Entry(payment).State != EntityState.Added && Entry(payment).State != EntityState.Modified)
                {
                    payment.RefreshConcurrencyStamp();
                }
            }
        }
        ChangeTracker.DetectChanges();
        return Task.CompletedTask;
    }

    public void EnsureDeleteAllowed(Type entityType) => throw new InvalidOperationException("Payment records cannot be deleted.");
    public void EnsureBulkWriteAllowed(Type entityType) => throw new InvalidOperationException("Payment writes require tracked aggregates.");
}
