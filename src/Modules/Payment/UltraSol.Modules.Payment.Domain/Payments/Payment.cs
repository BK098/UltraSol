using UltraSol.Shared.Domain.Common.Entities;

namespace UltraSol.Modules.Payment.Domain.Payments;

public sealed class Payment : AggregateRoot
{
    private readonly List<PaymentTransaction> _transactions = [];
    private readonly List<PaymentRefund> _refunds = [];
    private Payment() { }

    public Guid OrderId => Id;
    public long OrderVersion { get; private set; }
    public string OrderStatus { get; private set; } = "";
    public string OrderNumber { get; private set; } = "";
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "";
    public string PaymentTerm { get; private set; } = "";
    public int? NetDays { get; private set; }
    public DateTimeOffset? ReservationExpiresAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? DueAt { get; private set; }
    public bool HasSnapshot { get; private set; }
    public bool NoPaymentRequired => HasSnapshot && Amount == 0;
    public bool IsPaid => _transactions.Any(transaction => transaction.Status == PaymentTransactionStatus.Succeeded && transaction.Applied);
    public IReadOnlyCollection<PaymentTransaction> Transactions => _transactions.AsReadOnly();
    public IReadOnlyCollection<PaymentRefund> Refunds => _refunds.AsReadOnly();

    public static Payment Start(Guid orderId, DateTimeOffset now)
    {
        PaymentRule.Require(orderId != Guid.Empty, "OrderId is required.");
        var payment = new Payment { Id = orderId };
        payment.MarkCreated(null, now);
        return payment;
    }

    public void ApplySnapshot(long version, string status, string orderNumber, decimal amount, string currency, string paymentTerm,
        int? netDays, DateTimeOffset reservationExpiresAt, DateTimeOffset? completedAt, DateTimeOffset now)
    {
        PaymentRule.Require(version > 0, "Order version must be positive.");
        PaymentRule.Amount(amount, allowZero: true);
        currency = PaymentRule.Currency(currency);
        orderNumber = PaymentRule.Text(orderNumber, 64, "OrderNumber");
        PaymentRule.Require(paymentTerm is "Prepaid" or "COD" or "Net", "Invalid payment term.");
        PaymentRule.Require(paymentTerm == "Net" ? netDays is > 0 and <= 3650 : netDays is null, "Invalid net days.");
        PaymentRule.Require(reservationExpiresAt > DateTimeOffset.MinValue, "Reservation expiry is required.");
        if (HasSnapshot)
        {
            PaymentRule.Require(OrderNumber == orderNumber && Amount == amount && Currency == currency &&
                PaymentTerm == paymentTerm && NetDays == netDays && ReservationExpiresAt == reservationExpiresAt,
                "Order payment snapshot changed.", "PaymentConflict");
        }
        else
        {
            OrderNumber = orderNumber;
            Amount = amount;
            Currency = currency;
            PaymentTerm = paymentTerm;
            NetDays = netDays;
            ReservationExpiresAt = reservationExpiresAt;
            HasSnapshot = true;
        }
        ApplyOrderSignal(version, status, completedAt, now);
        SetDueAt();
        ApplyEligibleReceipt(now);
    }

    public void ApplyOrderSignal(long version, string status, DateTimeOffset? completedAt, DateTimeOffset now)
    {
        PaymentRule.Require(version > 0 && status is "Placed" or "Confirmed" or "Completed" or "Cancelled" or "Rejected",
            "Invalid order signal.");
        PaymentRule.Require(status != "Completed" || completedAt is not null, "Completed order requires completion time.");
        if (version < OrderVersion)
        {
            return;
        }
        if (version == OrderVersion)
        {
            PaymentRule.Require(OrderStatus == status, "Contradictory order signal.", "PaymentConflict");
            return;
        }
        OrderVersion = version;
        OrderStatus = status;
        if (status == "Completed")
        {
            CompletedAt ??= completedAt;
            SetDueAt();
        }
        if (status is "Cancelled" or "Rejected")
        {
            foreach (var receipt in _transactions.Where(transaction => transaction.Status == PaymentTransactionStatus.Succeeded))
            {
                RequestRefund(receipt, "Order " + status, now);
            }
        }
        MarkUpdated(null, now);
    }

    public PaymentTransaction CreateTransaction(string method, decimal amount, string currency, string idempotencyKey, string fingerprint,
        string source, string? reference, string? providerReference, Guid? actorId, string? note, DateTimeOffset now,
        DateTimeOffset? expiresAt = null)
    {
        PaymentRule.Require(HasSnapshot && Amount > 0, "Payment snapshot with a nonzero amount is required.");
        PaymentRule.Require(amount == Amount && PaymentRule.Currency(currency) == Currency, "Receipt must match order amount and currency.");
        var existing = _transactions.SingleOrDefault(transaction => transaction.IdempotencyKey == idempotencyKey);
        if (existing is not null)
        {
            PaymentRule.Require(existing.Method == method && existing.Amount == amount && existing.Currency == PaymentRule.Currency(currency) &&
                existing.RequestFingerprint == fingerprint && existing.Source == source && existing.Reference == reference &&
                existing.ProviderReference == providerReference, "Idempotency key conflicts with another payment request.", "PaymentConflict");
            return existing;
        }
        PaymentRule.Require(method != "COD" || PaymentTerm == "COD", "COD receipt requires COD terms.");
        PaymentRule.Require(method != "Vnpay" || PaymentTerm == "Prepaid" && OrderStatus is not ("Cancelled" or "Rejected") &&
            ReservationExpiresAt > now && !IsPaid, "VNPAY attempt is not available.");
        PaymentRule.Require(!_transactions.Any(transaction => reference is not null && transaction.Source == source &&
            transaction.Reference == reference), "Source reference already exists.", "PaymentConflict");
        PaymentRule.Require(!_transactions.Any(transaction => providerReference is not null &&
            transaction.ProviderReference == providerReference), "Provider reference already exists.", "PaymentConflict");
        PaymentRule.Require(method != "Vnpay" || !_transactions.Any(transaction => transaction.Method == "Vnpay" &&
            transaction.Status is PaymentTransactionStatus.Pending or PaymentTransactionStatus.NeedsReview),
            "A VNPAY attempt is unresolved.", "PaymentConflict");
        var receipt = new PaymentTransaction(Id, method, amount, currency, idempotencyKey, fingerprint, source, reference,
            providerReference, actorId, note, now, expiresAt);
        _transactions.Add(receipt);
        MarkUpdated(actorId?.ToString(), now);
        return receipt;
    }

    public void RecordTransactionSuccess(Guid transactionId, string? providerReference, string? providerTransactionNo, DateTimeOffset receivedAt)
    {
        var receipt = Transaction(transactionId);
        receipt.Succeed(providerReference, providerTransactionNo, receivedAt);
        if (HasSnapshot)
        {
            ApplyEligibleReceipt(receivedAt);
        }
        if (OrderStatus is "Cancelled" or "Rejected")
        {
            RequestRefund(receipt, "Order " + OrderStatus, receivedAt);
        }
        MarkUpdated(receipt.ActorId?.ToString(), receivedAt);
    }

    public void RecordTransactionFailure(Guid transactionId, DateTimeOffset nextCheckAt)
    {
        Transaction(transactionId).Fail(nextCheckAt);
        MarkUpdated(null, nextCheckAt);
    }

    public void MarkTransactionNeedsReview(Guid transactionId, DateTimeOffset nextCheckAt)
    {
        Transaction(transactionId).NeedsReview(nextCheckAt);
        MarkUpdated(null, nextCheckAt);
    }

    public void ReconcileTransaction(Guid transactionId, bool received, string evidenceReference, Guid actorId,
        DateTimeOffset receivedAt, string? providerTransactionNo, DateTimeOffset now)
    {
        PaymentRule.Require(actorId != Guid.Empty, "Reconciliation actor is required.");
        var receipt = Transaction(transactionId);
        receipt.Reconcile(received, evidenceReference, actorId, receivedAt, providerTransactionNo, now);
        if (received)
        {
            ApplyEligibleReceipt(now);
            if (OrderStatus is "Cancelled" or "Rejected")
            {
                RequestRefund(receipt, "Order " + OrderStatus, now);
            }
        }
        MarkUpdated(actorId.ToString(), now);
    }

    public void ApproveRefund(Guid refundId, Guid actorId, DateTimeOffset now)
    {
        Refund(refundId).Approve(actorId, now);
        MarkUpdated(actorId.ToString(), now);
    }

    public void BeginRefundProcessing(Guid refundId, string providerRequestId, DateTimeOffset now)
    {
        Refund(refundId).BeginProcessing(providerRequestId);
        MarkUpdated(null, now);
    }

    public void RecordRefundOutcome(Guid refundId, string outcome, DateTimeOffset now)
    {
        PaymentRule.Require(Enum.TryParse<PaymentRefundOutcome>(outcome, out var parsed), "Invalid refund outcome.");
        Refund(refundId).RecordOutcome(parsed, now);
        MarkUpdated(null, now);
    }

    public void ConfirmRefundSettled(Guid refundId, string evidenceReference, Guid actorId, DateTimeOffset now)
    {
        Refund(refundId).ConfirmSettled(evidenceReference, actorId, now);
        MarkUpdated(actorId.ToString(), now);
    }

    private PaymentTransaction Transaction(Guid id) => _transactions.SingleOrDefault(value => value.Id == id)
        ?? throw new UltraSol.Shared.Domain.Common.Exceptions.DomainException("Payment transaction was not found.", "PaymentNotFound");

    private PaymentRefund Refund(Guid id) => _refunds.SingleOrDefault(value => value.Id == id)
        ?? throw new UltraSol.Shared.Domain.Common.Exceptions.DomainException("Payment refund was not found.", "PaymentNotFound");

    private void ApplyEligibleReceipt(DateTimeOffset now)
    {
        if (!HasSnapshot || OrderStatus is "Cancelled" or "Rejected")
        {
            return;
        }
        foreach (var receipt in _transactions.Where(transaction => transaction.Status == PaymentTransactionStatus.Succeeded))
        {
            if (!IsPaid && receipt.Amount == Amount && receipt.Currency == Currency)
            {
                receipt.Apply();
            }
            else if (!receipt.Applied)
            {
                RequestRefund(receipt, "Unapplied payment", now);
            }
        }
    }

    private void RequestRefund(PaymentTransaction receipt, string reason, DateTimeOffset now)
    {
        if (_refunds.Any(refund => refund.TransactionId == receipt.Id))
        {
            return;
        }
        _refunds.Add(new PaymentRefund(Id, receipt, reason, now));
    }

    private void SetDueAt()
    {
        if (PaymentTerm == "Net" && CompletedAt is not null && DueAt is null)
        {
            DueAt = CompletedAt.Value.AddDays(NetDays!.Value);
        }
    }
}
