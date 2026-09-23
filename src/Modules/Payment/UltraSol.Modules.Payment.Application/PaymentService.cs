using System.Security.Cryptography;
using System.Text.Json;
using UltraSol.Modules.Payment.Application.Contracts;
using UltraSol.Modules.Payment.Domain.Payments;
using UltraSol.Shared.Application.Authentication;
using PaymentAggregate = UltraSol.Modules.Payment.Domain.Payments.Payment;

namespace UltraSol.Modules.Payment.Application;

public sealed record PaymentSummary(Guid OrderId, string Status, decimal Amount, string Currency, string PaymentTerm,
    DateTimeOffset? DueAt, bool IsOverdue, decimal ReceivedAmount, decimal RefundedAmount, int PendingRefunds);
public sealed record PaymentSession(Guid TransactionId, string PaymentUrl, DateTimeOffset ExpiresAt);
public sealed record ManualReceipt(string Method, decimal Amount, string Currency, DateTimeOffset ReceivedAt, string Source, string Reference, string? Note);
public sealed record PaymentDetails(PaymentSummary Summary, string ConcurrencyStamp, string OrderNumber, string OrderStatus, long OrderVersion,
    PaymentTransaction[] Transactions, PaymentRefund[] Refunds);
public sealed record VnpayAcknowledgement(string RspCode, string Message);

public sealed class PaymentService(IPaymentStore store, IPaymentUnitOfWork unit, IOrderingPaymentContextClient ordering,
    IVnpayGateway gateway, ICurrentAccount account, TimeProvider clock)
{
    public async Task<PaymentSummary> GetAsync(Guid orderId, CancellationToken ct)
    {
        await EnsureAsync(orderId, false, ct);
        return Summary((await store.GetAsync(orderId, ct))!);
    }

    public async Task<PaymentDetails> DetailAsync(Guid orderId, CancellationToken ct)
    {
        await EnsureAsync(orderId, false, ct);
        var payment = (await store.GetAsync(orderId, ct))!;
        return new(Summary(payment), payment.ConcurrencyStamp, payment.OrderNumber, payment.OrderStatus, payment.OrderVersion,
            payment.Transactions.OrderBy(value => value.CreatedAt).ToArray(), payment.Refunds.OrderBy(value => value.CreatedAt).ToArray());
    }

    public async Task<PaymentSummary[]> ListAsync(int skip, int take, bool overdue, CancellationToken ct) =>
        (await store.ListAsync(skip, take, overdue, clock.GetUtcNow(), ct)).Select(Summary).ToArray();

    public async Task<PaymentTransaction> RecordAsync(Guid orderId, string key, ManualReceipt receipt, CancellationToken ct)
    {
        var actor = Actor();
        key = Text(key, 128, "Idempotency-Key");
        receipt = receipt with { Currency = Text(receipt.Currency, 3, "Currency").ToUpperInvariant(), Source = Text(receipt.Source, 100, "Source").ToUpperInvariant(),
            Reference = Text(receipt.Reference, 200, "Reference").ToUpperInvariant(), Note = receipt.Note?.Trim(), ReceivedAt = Microseconds(receipt.ReceivedAt) };
        Require(receipt.Method is "Cash" or "BankTransfer" or "COD", "Unknown manual collection method.");
        Require(receipt.ReceivedAt != default && receipt.ReceivedAt <= clock.GetUtcNow().AddMinutes(5), "Invalid receipt time.");
        Require(receipt.Note is null || receipt.Note.Length <= 2000, "Note is too long.");
        await EnsureAsync(orderId, false, ct);
        return await ChangeAsync(orderId, payment =>
        {
            var fingerprint = Fingerprint(new { orderId, receipt });
            var replay = payment.Transactions.SingleOrDefault(value => value.IdempotencyKey == key);
            if (replay is not null)
            {
                Conflict(replay.RequestFingerprint == fingerprint, "Idempotency key was used for different data.");
                return replay;
            }
            Conflict(receipt.Amount == payment.Amount && receipt.Currency == payment.Currency, "Receipt must match the full order amount and currency.");
            var transaction = payment.CreateTransaction(receipt.Method, receipt.Amount, receipt.Currency, key, fingerprint, receipt.Source,
                receipt.Reference, null, actor, receipt.Note, clock.GetUtcNow());
            payment.RecordTransactionSuccess(transaction.Id, null, null, receipt.ReceivedAt);
            return transaction;
        }, ct);
    }

    public async Task<PaymentSession> CreateSessionAsync(Guid orderId, string key, string ipAddress, CancellationToken ct)
    {
        key = Text(key, 128, "Idempotency-Key");
        Require(System.Net.IPAddress.TryParse(ipAddress, out _), "Invalid client IP address.");
        await EnsureAsync(orderId, true, ct);
        return await ChangeAsync(orderId, payment =>
        {
            var fingerprint = Fingerprint(new { orderId, method = "Vnpay" });
            var transaction = payment.Transactions.SingleOrDefault(value => value.IdempotencyKey == key);
            if (transaction is not null)
            {
                Conflict(transaction.RequestFingerprint == fingerprint && transaction.Method == "Vnpay", "Idempotency key was used for different data.");
                return Session(transaction);
            }
            var now = clock.GetUtcNow();
            Conflict(payment.HasSnapshot && payment.PaymentTerm == "Prepaid" && !payment.IsPaid && !payment.NoPaymentRequired
                && payment.OrderStatus is "Placed" or "Confirmed" && payment.ReservationExpiresAt > now.AddSeconds(1), "Order is not eligible for VNPAY.");
            var reference = Guid.CreateVersion7().ToString("N");
            var expires = payment.ReservationExpiresAt!.Value;
            var specification = new VnpayPayment(reference, payment.Amount, payment.Currency, now, expires, ipAddress);
            var url = gateway.CreateUrl(specification);
            transaction = payment.CreateTransaction("Vnpay", payment.Amount, payment.Currency, key, fingerprint, "VNPAY", null,
                reference, null, null, now, expires);
            transaction.MarkGatewayCreated(now, ipAddress);
            return new PaymentSession(transaction.Id, url, expires);
        }, ct);
    }

    public async Task<VnpayAcknowledgement> IpnAsync(IReadOnlyDictionary<string, string> values, CancellationToken ct)
    {
        var result = gateway.VerifyCallback(values);
        if (!result.Verified)
        {
            return new("97", "Invalid signature or merchant");
        }
        store.Reset();
        var found = await store.FindByProviderReferenceAsync(result.Reference, ct);
        if (found is null)
        {
            return new("01", "Transaction not found");
        }
        return await ChangeAsync(found.Id, payment => ApplyGatewayResult(payment, result, false), ct);
    }

    public object Return(IReadOnlyDictionary<string, string> values)
    {
        var result = gateway.VerifyCallback(values);
        return new { verified = result.Verified, result = !result.Verified ? "Unverified" : result.ResponseCode == "00" && result.Status == "00" ? "AwaitingConfirmation" : "NotConfirmed" };
    }

    public Task<PaymentRefund> ApproveAsync(Guid orderId, Guid refundId, string stamp, CancellationToken ct)
    {
        var actor = Actor();
        return ChangeAsync(orderId, payment =>
        {
            Conflict(payment.ConcurrencyStamp == stamp, "Payment changed; reload before approval.");
            payment.ApproveRefund(refundId, actor, clock.GetUtcNow());
            return payment.Refunds.Single(value => value.Id == refundId);
        }, ct);
    }

    public Task<PaymentRefund> ConfirmRefundAsync(Guid orderId, Guid refundId, string stamp, string evidence, CancellationToken ct)
    {
        var actor = Actor();
        evidence = Text(evidence, 500, "EvidenceReference");
        return ChangeAsync(orderId, payment =>
        {
            Conflict(payment.ConcurrencyStamp == stamp, "Payment changed; reload before reconciliation.");
            var refund = payment.Refunds.SingleOrDefault(value => value.Id == refundId) ?? throw Missing();
            if (refund.State == PaymentRefundState.Approved)
            {
                payment.BeginRefundProcessing(refundId, refund.Id.ToString("N"), clock.GetUtcNow());
            }
            payment.ConfirmRefundSettled(refundId, evidence, actor, clock.GetUtcNow());
            return refund;
        }, ct);
    }

    public Task<PaymentTransaction> ReconcileAsync(Guid orderId, Guid transactionId, string stamp, bool received, string evidence,
        DateTimeOffset receivedAt, string? providerTransactionNo, CancellationToken ct)
    {
        var actor = Actor();
        evidence = Text(evidence, 500, "EvidenceReference");
        Require(!received || receivedAt != default && receivedAt <= clock.GetUtcNow().AddMinutes(5), "Invalid receipt time.");
        return ChangeAsync(orderId, payment =>
        {
            Conflict(payment.ConcurrencyStamp == stamp, "Payment changed; reload before reconciliation.");
            var transaction = payment.Transactions.SingleOrDefault(value => value.Id == transactionId) ?? throw Missing();
            Conflict(transaction.Method == "Vnpay", "Only gateway transactions require payment reconciliation.");
            Require(!received || !string.IsNullOrWhiteSpace(providerTransactionNo), "Provider transaction number is required.");
            payment.ReconcileTransaction(transactionId, received, evidence, actor, Microseconds(receivedAt), providerTransactionNo, clock.GetUtcNow());
            return transaction;
        }, ct);
    }

    public Task<T> ChangeAsync<T>(Guid orderId, Func<PaymentAggregate, T> action, CancellationToken ct)
    {
        store.Reset();
        return unit.ExecuteInTransactionAsync(async token =>
        {
            await store.LockAsync("payment:" + orderId, token);
            var payment = await store.GetAsync(orderId, token) ?? throw Missing();
            return action(payment);
        }, ct);
    }

    public VnpayAcknowledgement ApplyGatewayResult(PaymentAggregate payment, VnpayResult result, bool query)
    {
        var transaction = payment.Transactions.SingleOrDefault(value => value.ProviderReference == result.Reference);
        if (transaction is null)
        {
            return new("01", "Transaction not found");
        }
        var next = clock.GetUtcNow().AddMinutes(5);
        if (!result.Verified || result.Amount != transaction.Amount || result.Currency is not (null or "" or "VND")
            || transaction.Currency != "VND" || query && result.TransactionType != "01")
        {
            payment.MarkTransactionNeedsReview(transaction.Id, next);
            return new("04", "Reconciliation required");
        }
        if (result.ResponseCode == "00" && result.Status == "00" && !string.IsNullOrWhiteSpace(result.TransactionNo) && result.TransactionNo != "0")
        {
            if (transaction.Status == PaymentTransactionStatus.Succeeded)
            {
                if (transaction.ProviderTransactionNo != result.TransactionNo)
                {
                    payment.MarkTransactionNeedsReview(transaction.Id, next);
                    return new("99", "Conflicting transaction identity");
                }
                return new("02", "Already confirmed");
            }
            payment.RecordTransactionSuccess(transaction.Id, result.Reference, result.TransactionNo, result.PaidAt ?? clock.GetUtcNow());
            return new("00", "Confirmed");
        }
        if (result.Status == "02" && (query ? result.ResponseCode == "00" : result.ResponseCode != "00")
            && transaction.Status != PaymentTransactionStatus.Succeeded)
        {
            payment.RecordTransactionFailure(transaction.Id, next);
            return new("00", "Failure confirmed");
        }
        payment.MarkTransactionNeedsReview(transaction.Id, next);
        return new("00", "Pending reconciliation");
    }

    public static VnpayPayment GatewayPayment(PaymentTransaction transaction) => new(transaction.ProviderReference!, transaction.Amount,
        transaction.Currency, transaction.GatewayCreatedAt ?? transaction.CreatedAt, transaction.ExpiresAt!.Value, transaction.ClientIp!);

    private PaymentSession Session(PaymentTransaction transaction) => new(transaction.Id, gateway.CreateUrl(GatewayPayment(transaction)), transaction.ExpiresAt!.Value);

    private async Task EnsureAsync(Guid orderId, bool refresh, CancellationToken ct)
    {
        Require(orderId != Guid.Empty, "OrderId is required.");
        store.Reset();
        var existing = await store.GetAsync(orderId, ct);
        if (existing is { HasSnapshot: true } && !refresh)
        {
            return;
        }
        var context = await ordering.GetAsync(orderId, ct);
        Conflict(context.OrderId == orderId, "Ordering returned a different order.");
        store.Reset();
        await unit.ExecuteInTransactionAsync(async token =>
        {
            await store.LockAsync("payment:" + orderId, token);
            var payment = await store.GetAsync(orderId, token);
            if (payment is null)
            {
                payment = PaymentAggregate.Start(orderId, clock.GetUtcNow());
                store.Add(payment);
            }
            payment.ApplySnapshot(context.OrderVersion, context.OrderStatus, context.OrderNumber, context.Amount, context.Currency,
                context.PaymentTerm, context.NetDays, context.ReservationExpiresAt, context.CompletedAt, clock.GetUtcNow());
        }, ct);
    }

    private PaymentSummary Summary(PaymentAggregate payment)
    {
        var pendingRefunds = payment.Refunds.Count(value => value.State != PaymentRefundState.Refunded);
        var status = !payment.HasSnapshot ? "AwaitingOrder" : payment.NoPaymentRequired ? "NoPaymentRequired" : pendingRefunds > 0 ? "RefundPending"
            : payment.Refunds.Count > 0 && payment.OrderStatus is "Cancelled" or "Rejected" ? "Refunded" : payment.IsPaid ? "Paid" : "Unpaid";
        return new(payment.Id, status, payment.Amount, payment.Currency, payment.PaymentTerm, payment.DueAt,
            payment.HasSnapshot && payment.Amount > 0 && !payment.IsPaid && payment.OrderStatus is not ("Cancelled" or "Rejected") && payment.DueAt < clock.GetUtcNow(),
            payment.Transactions.Where(value => value.Status == PaymentTransactionStatus.Succeeded).Sum(value => value.Amount),
            payment.Refunds.Where(value => value.State == PaymentRefundState.Refunded).Sum(value => value.Amount), pendingRefunds);
    }

    private Guid Actor() => account.UserId is { } actor && actor != Guid.Empty ? actor : throw new PaymentFailure(401, "Unauthenticated", "Sign in is required.");
    private static PaymentFailure Missing() => new(404, "PaymentNotFound", "Payment record was not found.");
    private static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static DateTimeOffset Microseconds(DateTimeOffset value) => new(value.UtcTicks / 10 * 10, TimeSpan.Zero);
    private static string Text(string? value, int maximum, string name)
    {
        Require(!string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximum, name + " is required and exceeds no allowed length.");
        return value!.Trim();
    }
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new PaymentFailure(400, "InvalidPaymentRequest", message);
        }
    }
    private static void Conflict(bool condition, string message)
    {
        if (!condition)
        {
            throw new PaymentFailure(409, "PaymentConflict", message);
        }
    }
}
