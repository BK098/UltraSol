using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Payment.Application.Contracts;
using UltraSol.Modules.Payment.Domain.Payments;
using UltraSol.Modules.Payment.Infrastructure.Persistence;
using PaymentAggregate = UltraSol.Modules.Payment.Domain.Payments.Payment;

namespace UltraSol.Modules.Payment.Infrastructure.Repositories;

public sealed class PaymentStore(PaymentDbContext context) : IPaymentStore
{
    public void Reset() => context.ChangeTracker.Clear();

    public async Task LockAsync(string key, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Payment locks require an active transaction.");
        }
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", cancellationToken);
    }

    public async Task<PaymentAggregate?> GetAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var payment = await context.Payments.SingleOrDefaultAsync(value => value.Id == orderId, cancellationToken);
        if (payment is not null)
        {
            await context.MaterializeAsync(payment, cancellationToken);
        }
        return payment;
    }

    public async Task<PaymentAggregate?> FindByProviderReferenceAsync(string reference, CancellationToken cancellationToken)
    {
        var orderId = await context.Transactions.AsNoTracking().Where(value => value.ProviderReference == reference)
            .Select(value => (Guid?)value.PaymentId).SingleOrDefaultAsync(cancellationToken);
        return orderId is null ? null : await GetAsync(orderId.Value, cancellationToken);
    }

    public Task<Guid[]> PendingGatewayOrderIdsAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        context.Transactions.AsNoTracking().Where(value => value.Method == "Vnpay" &&
            (value.Status == PaymentTransactionStatus.Pending || value.Status == PaymentTransactionStatus.NeedsReview) &&
            (value.NextCheckAt == null || value.NextCheckAt <= now) && (value.LeaseUntil == null || value.LeaseUntil <= now))
            .OrderBy(value => value.CreatedAt).Select(value => value.PaymentId).Distinct().Take(20).ToArrayAsync(cancellationToken);

    public Task<Guid[]> PendingRefundOrderIdsAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        context.Refunds.AsNoTracking().Where(value => value.State == PaymentRefundState.Approved ||
            value.State == PaymentRefundState.Processing)
            .Where(value =>
            (value.NextCheckAt == null || value.NextCheckAt <= now) && (value.LeaseUntil == null || value.LeaseUntil <= now))
            .OrderBy(value => value.CreatedAt).Select(value => value.PaymentId).Distinct().Take(20).ToArrayAsync(cancellationToken);

    public async Task<PaymentAggregate[]> ListAsync(int skip, int take, bool overdue, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);
        var query = context.Payments.AsQueryable();
        if (overdue)
        {
            query = query.Where(value => value.HasSnapshot && value.Amount > 0 && value.OrderStatus != "Cancelled" &&
                value.OrderStatus != "Rejected" && value.DueAt != null && value.DueAt < now &&
                !context.Transactions.Any(transaction => transaction.PaymentId == value.Id &&
                    transaction.Status == PaymentTransactionStatus.Succeeded && transaction.Applied));
        }
        var payments = await query.OrderByDescending(value => value.CreatedAt).ThenBy(value => value.Id)
            .Skip(skip).Take(Math.Min(take, 100)).ToArrayAsync(cancellationToken);
        foreach (var payment in payments)
        {
            await context.MaterializeAsync(payment, cancellationToken);
        }
        return payments;
    }

    public Task<PaymentRefund[]> ListRefundsAsync(int skip, int take, string? state, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);
        var query = context.Refunds.AsNoTracking();
        if (state is not null)
        {
            if (!Enum.TryParse<PaymentRefundState>(state, out var parsed) || !Enum.IsDefined(parsed))
            {
                throw new ArgumentException("Invalid refund state.", nameof(state));
            }
            query = query.Where(value => value.State == parsed);
        }
        return query.OrderByDescending(value => value.CreatedAt).ThenBy(value => value.Id)
            .Skip(skip).Take(Math.Min(take, 100)).ToArrayAsync(cancellationToken);
    }

    public void Add(PaymentAggregate payment) => context.Payments.Add(payment);
}
