using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Payment.Domain.Payments;
using UltraSol.Shared.Infrastructure.Persistence.Extensions;
using PaymentAggregate = UltraSol.Modules.Payment.Domain.Payments.Payment;

namespace UltraSol.Modules.Payment.Infrastructure.Persistence;

internal static class PaymentModel
{
    internal static void Configure(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema.Name);
        var payment = ModelConfigure.Root<PaymentAggregate>(model, "payments");
        payment.Ignore(value => value.OrderId);
        payment.Ignore(value => value.NoPaymentRequired);
        payment.Ignore(value => value.IsPaid);
        payment.Property(value => value.OrderStatus).HasMaxLength(32);
        payment.Property(value => value.OrderNumber).HasMaxLength(64);
        payment.Property(value => value.Amount).HasColumnType("numeric(18,2)");
        payment.Property(value => value.Currency).HasMaxLength(3);
        payment.Property(value => value.PaymentTerm).HasMaxLength(16);
        payment.HasMany(value => value.Transactions).WithOne().HasForeignKey(value => value.PaymentId).OnDelete(DeleteBehavior.Restrict);
        payment.HasMany(value => value.Refunds).WithOne().HasForeignKey(value => value.PaymentId).OnDelete(DeleteBehavior.Restrict);
        payment.Navigation(value => value.Transactions).HasField("_transactions").UsePropertyAccessMode(PropertyAccessMode.Field);
        payment.Navigation(value => value.Refunds).HasField("_refunds").UsePropertyAccessMode(PropertyAccessMode.Field);
        payment.HasIndex(value => value.OrderNumber).IsUnique().HasFilter("has_snapshot").HasDatabaseName("ux_payments_order_number");
        payment.ToTable("payments", table => table.HasCheckConstraint("ck_payments_amount", "amount >= 0"));

        var transaction = ModelConfigure.Entity<PaymentTransaction>(model, "transactions");
        transaction.Ignore(value => value.OrderId);
        transaction.Property(value => value.Method).HasMaxLength(32);
        transaction.Property(value => value.Amount).HasColumnType("numeric(18,2)");
        transaction.Property(value => value.Currency).HasMaxLength(3);
        transaction.Property(value => value.IdempotencyKey).HasMaxLength(200);
        transaction.Property(value => value.RequestFingerprint).HasMaxLength(128);
        transaction.Property(value => value.Source).HasMaxLength(100);
        transaction.Property(value => value.Reference).HasMaxLength(200);
        transaction.Property(value => value.ProviderReference).HasMaxLength(200);
        transaction.Property(value => value.ProviderTransactionNo).HasMaxLength(200);
        transaction.Property(value => value.ClientIp).HasMaxLength(45);
        transaction.Property(value => value.Note).HasMaxLength(4000);
        transaction.Property(value => value.ReviewReason).HasMaxLength(500);
        transaction.Property(value => value.EvidenceReference).HasMaxLength(500);
        transaction.Property(value => value.Status).HasConversion<int>();
        transaction.HasIndex(value => new { value.PaymentId, value.IdempotencyKey }).IsUnique().HasDatabaseName("ux_transactions_payment_key");
        transaction.HasIndex(value => new { value.Source, value.Reference }).IsUnique().HasFilter("reference IS NOT NULL")
            .HasDatabaseName("ux_transactions_source_reference");
        transaction.HasIndex(value => value.ProviderReference).IsUnique().HasFilter("provider_reference IS NOT NULL")
            .HasDatabaseName("ux_transactions_provider_reference");
        transaction.HasIndex(value => value.PaymentId).IsUnique().HasFilter("method = 'Vnpay' AND status IN (0, 3)")
            .HasDatabaseName("ux_transactions_one_unresolved_vnpay");
        transaction.HasIndex(value => new { value.Status, value.NextCheckAt, value.LeaseUntil, value.CreatedAt })
            .HasDatabaseName("ix_transactions_recovery");
        transaction.ToTable("transactions", table => table.HasCheckConstraint("ck_transactions_amount", "amount > 0"));

        var refund = ModelConfigure.Entity<PaymentRefund>(model, "refunds");
        refund.Property(value => value.Amount).HasColumnType("numeric(18,2)");
        refund.Property(value => value.Currency).HasMaxLength(3);
        refund.Property(value => value.Reason).HasMaxLength(2000);
        refund.Property(value => value.State).HasConversion<int>();
        refund.Property(value => value.ProviderRequestId).HasMaxLength(200);
        refund.Property(value => value.Outcome).HasConversion<int>();
        refund.Property(value => value.EvidenceReference).HasMaxLength(500);
        refund.HasOne<PaymentTransaction>().WithMany().HasForeignKey(value => value.TransactionId).OnDelete(DeleteBehavior.Restrict);
        refund.HasIndex(value => value.TransactionId).IsUnique().HasDatabaseName("ux_refunds_transaction");
        refund.HasIndex(value => value.ProviderRequestId).IsUnique().HasFilter("provider_request_id IS NOT NULL")
            .HasDatabaseName("ux_refunds_provider_request");
        refund.HasIndex(value => new { value.State, value.NextCheckAt, value.LeaseUntil, value.CreatedAt })
            .HasDatabaseName("ix_refunds_recovery");
        refund.ToTable("refunds", table => table.HasCheckConstraint("ck_refunds_amount", "amount > 0"));
    }
}
