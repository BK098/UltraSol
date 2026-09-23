using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Payment.Application;
using UltraSol.Modules.Payment.Domain.Payments;
using UltraSol.Modules.Payment.Infrastructure.Persistence;
using UltraSol.Modules.Payment.Infrastructure.Repositories;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Infrastructure.Messaging;
using Xunit;
using PaymentAggregate = UltraSol.Modules.Payment.Domain.Payments.Payment;

namespace UltraSol.Modules.Payment.Tests;

public sealed class PaymentPersistenceTests(PaymentDatabaseFixture fixture) : IClassFixture<PaymentDatabaseFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 0, 0, 0, TimeSpan.Zero);

    [PaymentPostgresFact]
    public async Task Writes_require_owned_transaction_and_loaded_aggregate()
    {
        var payment = NewPayment();
        await using (var db = fixture.Create())
        {
            new PaymentStore(db).Add(payment);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        await using (var db = fixture.Create())
        {
            var store = new PaymentStore(db);
            await Unit(db).ExecuteInTransactionAsync(_ =>
            {
                store.Add(payment);
                return Task.CompletedTask;
            });
        }
        await using var read = fixture.Create();
        var loaded = await new PaymentStore(read).GetAsync(payment.Id, default);
        Assert.NotNull(loaded);
        Assert.True(loaded.HasSnapshot);
        Assert.Equal(payment.Amount, loaded.Amount);
    }

    [PaymentPostgresFact]
    public async Task Unique_manual_reference_and_stale_approval_stamp_conflict()
    {
        var first = NewPayment();
        var second = NewPayment();
        first.CreateTransaction("BankTransfer", 100, "VND", "key-1", "fp-1", "bank", "shared-ref", null, null, null, Now);
        second.CreateTransaction("BankTransfer", 100, "VND", "key-2", "fp-2", "bank", "shared-ref", null, null, null, Now);
        await AddAsync(first);
        await using (var db = fixture.Create())
        {
            var failure = await Assert.ThrowsAsync<PaymentFailure>(() => Unit(db).ExecuteInTransactionAsync(_ =>
            {
                new PaymentStore(db).Add(second);
                return Task.CompletedTask;
            }));
            Assert.Equal(409, failure.Status);
        }
        await using var left = fixture.Create();
        await using var right = fixture.Create();
        var a = await new PaymentStore(left).GetAsync(first.Id, default) ?? throw new InvalidOperationException();
        var b = await new PaymentStore(right).GetAsync(first.Id, default) ?? throw new InvalidOperationException();
        a.CreateTransaction("BankTransfer", 100, "VND", "key-3", "fp-3", "bank", "ref-3", null, null, null, Now);
        await Unit(left).ExecuteInTransactionAsync(_ => Task.CompletedTask);
        b.CreateTransaction("BankTransfer", 100, "VND", "key-4", "fp-4", "bank", "ref-4", null, null, null, Now);
        var stale = await Assert.ThrowsAsync<PaymentFailure>(() => Unit(right).ExecuteInTransactionAsync(_ => Task.CompletedTask));
        Assert.Equal(409, stale.Status);
    }

    [PaymentPostgresFact]
    public async Task Concurrent_refund_approvals_cannot_both_commit()
    {
        var payment = NewPayment();
        var receipt = payment.CreateTransaction("Vnpay", 100, "VND", "refund-key", "refund-fp", "Vnpay", null,
            "refund-provider", null, null, Now);
        payment.RecordTransactionSuccess(receipt.Id, "refund-provider", "refund-txn", Now);
        payment.ApplyOrderSignal(2, "Cancelled", null, Now);
        await AddAsync(payment);
        await using var left = fixture.Create();
        await using var right = fixture.Create();
        var a = await new PaymentStore(left).GetAsync(payment.Id, default) ?? throw new InvalidOperationException();
        var b = await new PaymentStore(right).GetAsync(payment.Id, default) ?? throw new InvalidOperationException();
        var refundId = Assert.Single(a.Refunds).Id;
        a.ApproveRefund(refundId, Guid.NewGuid(), Now);
        await Unit(left).ExecuteInTransactionAsync(_ => Task.CompletedTask);
        b.ApproveRefund(refundId, Guid.NewGuid(), Now);
        var conflict = await Assert.ThrowsAsync<PaymentFailure>(() => Unit(right).ExecuteInTransactionAsync(_ => Task.CompletedTask));
        Assert.Equal(409, conflict.Status);
    }

    [PaymentPostgresFact]
    public async Task Rollback_includes_inbox_and_confirmed_money_cannot_be_changed()
    {
        var payment = NewPayment();
        await using (var db = fixture.Create())
        {
            var unit = Unit(db);
            await Assert.ThrowsAsync<InvalidOperationException>(() => unit.ExecuteInTransactionAsync(async ct =>
            {
                new PaymentStore(db).Add(payment);
                db.Set<InboxMessage>().Add(new InboxMessage { Id = Guid.NewGuid(), ReceivedAt = Now });
                await unit.SaveChangesAsync(ct);
                throw new InvalidOperationException("rollback");
            }));
        }
        await using (var db = fixture.Create())
        {
            Assert.False(await db.Payments.AnyAsync(value => value.Id == payment.Id));
            Assert.False(await db.Set<InboxMessage>().AnyAsync());
        }
        await AddAsync(payment);
        await using var update = fixture.Create();
        var loaded = await new PaymentStore(update).GetAsync(payment.Id, default) ?? throw new InvalidOperationException();
        update.Entry(loaded).Property(value => value.Amount).CurrentValue = 1m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Unit(update).ExecuteInTransactionAsync(_ => Task.CompletedTask));
    }

    [PaymentPostgresFact]
    public async Task Migration_matches_model_and_foreign_keys_stay_in_payment_schema()
    {
        await using var db = fixture.Create();
        Assert.Equal(await db.Database.GetAppliedMigrationsAsync(), db.Database.GetMigrations());
        Assert.False(db.Database.HasPendingModelChanges());
        var schemas = await db.Database.SqlQueryRaw<string>("SELECT DISTINCT target_ns.nspname::text AS \"Value\" FROM pg_constraint c JOIN pg_class source ON source.oid = c.conrelid JOIN pg_namespace source_ns ON source_ns.oid = source.relnamespace JOIN pg_class target ON target.oid = c.confrelid JOIN pg_namespace target_ns ON target_ns.oid = target.relnamespace WHERE c.contype = 'f' AND source_ns.nspname = 'payment'").ToArrayAsync();
        Assert.All(schemas, schema => Assert.Equal("payment", schema));
    }

    private async Task AddAsync(PaymentAggregate payment)
    {
        await using var db = fixture.Create();
        await Unit(db).ExecuteInTransactionAsync(_ =>
        {
            new PaymentStore(db).Add(payment);
            return Task.CompletedTask;
        });
    }

    private static PaymentAggregate NewPayment()
    {
        var payment = PaymentAggregate.Start(Guid.NewGuid(), Now);
        payment.ApplySnapshot(1, "Placed", "ORD-" + Guid.NewGuid().ToString("N"), 100, "VND", "Prepaid", null,
            Now.AddMinutes(15), null, Now);
        return payment;
    }

    private static PaymentUnitOfWork Unit(PaymentDbContext db) => new(db, new NullDispatcher());

    private sealed class NullDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
