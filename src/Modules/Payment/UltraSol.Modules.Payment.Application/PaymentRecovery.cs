using UltraSol.Modules.Payment.Application.Contracts;
using UltraSol.Modules.Payment.Domain.Payments;

namespace UltraSol.Modules.Payment.Application;

public sealed class PaymentRecovery(IPaymentStore store, PaymentService service, IVnpayGateway gateway, TimeProvider clock)
{
    public async Task QueryAsync(Guid orderId, Guid? transactionId, CancellationToken ct)
    {
        var claim = await service.ChangeAsync(orderId, payment =>
        {
            var now = clock.GetUtcNow();
            var transaction = payment.Transactions.FirstOrDefault(value => value.Method == "Vnpay" &&
                (transactionId is null ? value.Status is PaymentTransactionStatus.Pending or PaymentTransactionStatus.NeedsReview : value.Id == transactionId)
                && (value.LeaseUntil is null || value.LeaseUntil <= now)
                && (transactionId is not null || value.NextCheckAt is null || value.NextCheckAt <= now));
            return transaction is null ? null : new QueryClaim(transaction.Id, transaction.ClaimLease(now, now.AddMinutes(1)), PaymentService.GatewayPayment(transaction));
        }, ct);
        if (claim is null)
        {
            return;
        }
        var result = await gateway.QueryAsync(claim.Payment, ct);
        await service.ChangeAsync(orderId, payment =>
        {
            var transaction = payment.Transactions.Single(value => value.Id == claim.Id);
            if (transaction.LeaseToken != claim.Lease)
            {
                return false;
            }
            transaction.ReleaseLease(claim.Lease, clock.GetUtcNow().AddMinutes(5));
            if (result is not null)
            {
                if (result.Reference != claim.Payment.Reference)
                {
                    payment.MarkTransactionNeedsReview(transaction.Id, clock.GetUtcNow().AddMinutes(5));
                }
                else
                {
                    service.ApplyGatewayResult(payment, result, true);
                }
            }
            return true;
        }, ct);
    }

    public async Task RefundAsync(Guid orderId, CancellationToken ct)
    {
        var claim = await service.ChangeAsync(orderId, payment =>
        {
            var now = clock.GetUtcNow();
            var refund = payment.Refunds.FirstOrDefault(value => value.State is PaymentRefundState.Approved or PaymentRefundState.Processing
                && (value.LeaseUntil is null || value.LeaseUntil <= now) && (value.NextCheckAt is null || value.NextCheckAt <= now)
                && payment.Transactions.Any(transaction => transaction.Id == value.TransactionId && transaction.Method == "Vnpay"));
            if (refund is null)
            {
                return null;
            }
            var transaction = payment.Transactions.Single(value => value.Id == refund.TransactionId);
            var specification = PaymentService.GatewayPayment(transaction);
            gateway.CreateUrl(specification);
            var send = refund.State == PaymentRefundState.Approved;
            if (send)
            {
                payment.BeginRefundProcessing(refund.Id, refund.Id.ToString("N"), now);
            }
            return new RefundClaim(refund.Id, refund.ClaimLease(now, now.AddMinutes(1)), send,
                new(refund.ProviderRequestId!, specification, transaction.ProviderTransactionNo!, refund.ApprovedBy!.Value, now));
        }, ct);
        if (claim is null)
        {
            return;
        }
        // A persisted Processing intent may already have reached the provider. Recovery only queries.
        var result = claim.Send ? await gateway.RefundAsync(claim.Request, ct) : await gateway.QueryAsync(claim.Request.Payment, ct);
        await service.ChangeAsync(orderId, payment =>
        {
            var refund = payment.Refunds.Single(value => value.Id == claim.Id);
            if (refund.LeaseToken != claim.Lease || refund.State != PaymentRefundState.Processing)
            {
                return false;
            }
            var verified = result is { Verified: true } && result.Reference == claim.Request.Payment.Reference
                && result.Amount == refund.Amount && result.Currency is null or "" or "VND";
            var outcome = verified && result!.ResponseCode is "00" or "94" ? "Pending"
                : verified && result!.ResponseCode is "02" or "03" or "95" ? "Rejected" : "Unknown";
            payment.RecordRefundOutcome(refund.Id, outcome, clock.GetUtcNow());
            refund.ReleaseLease(claim.Lease, clock.GetUtcNow().AddMinutes(10));
            return true;
        }, ct);
    }

    public async Task<Guid[]> PendingAsync(CancellationToken ct) =>
        (await store.PendingGatewayOrderIdsAsync(clock.GetUtcNow(), ct)).Concat(await store.PendingRefundOrderIdsAsync(clock.GetUtcNow(), ct)).Distinct().ToArray();

    private sealed record QueryClaim(Guid Id, Guid Lease, VnpayPayment Payment);
    private sealed record RefundClaim(Guid Id, Guid Lease, bool Send, VnpayRefund Request);
}
