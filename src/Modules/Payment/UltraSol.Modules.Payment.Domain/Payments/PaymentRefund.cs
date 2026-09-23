using UltraSol.Shared.Domain.Common.Entities;

namespace UltraSol.Modules.Payment.Domain.Payments;

public enum PaymentRefundState { Requested, Approved, Processing, Refunded }
public enum PaymentRefundOutcome { Unknown, Rejected, Pending, Settled }

public sealed class PaymentRefund : BaseEntity
{
    private PaymentRefund() { }
    internal PaymentRefund(Guid paymentId, PaymentTransaction receipt, string reason, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        PaymentId = paymentId;
        TransactionId = receipt.Id;
        Amount = receipt.Amount;
        Currency = receipt.Currency;
        Reason = PaymentRule.Text(reason, 2000, "RefundReason");
        CreatedAt = now;
    }

    public Guid PaymentId { get; private set; }
    public Guid TransactionId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public PaymentRefundState State { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? ProviderRequestId { get; private set; }
    public PaymentRefundOutcome Outcome { get; private set; }
    public string? EvidenceReference { get; private set; }
    public Guid? ReconciledBy { get; private set; }
    public DateTimeOffset? ReconciledAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public DateTimeOffset? NextCheckAt { get; private set; }

    internal void Approve(Guid actorId, DateTimeOffset now)
    {
        PaymentRule.Require(State == PaymentRefundState.Requested && actorId != Guid.Empty, "Refund cannot be approved.", "PaymentConflict");
        ApprovedBy = actorId;
        ApprovedAt = now;
        State = PaymentRefundState.Approved;
    }

    internal void BeginProcessing(string requestId)
    {
        PaymentRule.Require(State == PaymentRefundState.Approved, "Refund must be approved before processing.", "PaymentConflict");
        ProviderRequestId = PaymentRule.Text(requestId, 200, "ProviderRequestId");
        State = PaymentRefundState.Processing;
    }

    internal void RecordOutcome(PaymentRefundOutcome outcome, DateTimeOffset now)
    {
        PaymentRule.Require(State == PaymentRefundState.Processing, "Refund is not processing.", "PaymentConflict");
        PaymentRule.Require(outcome is PaymentRefundOutcome.Unknown or PaymentRefundOutcome.Rejected or PaymentRefundOutcome.Pending,
            "Staff evidence is required to settle a refund.");
        Outcome = outcome;
        NextCheckAt = now;
    }

    internal void ConfirmSettled(string evidence, Guid actorId, DateTimeOffset now)
    {
        PaymentRule.Require(State == PaymentRefundState.Processing && actorId != Guid.Empty, "Refund is not ready for reconciliation.", "PaymentConflict");
        EvidenceReference = PaymentRule.Text(evidence, 500, "EvidenceReference");
        ReconciledBy = actorId;
        ReconciledAt = now;
        Outcome = PaymentRefundOutcome.Settled;
        State = PaymentRefundState.Refunded;
        LeaseToken = null;
        LeaseUntil = null;
        NextCheckAt = null;
    }

    public Guid ClaimLease(DateTimeOffset now, DateTimeOffset until)
    {
        PaymentRule.Require(until > now && (LeaseUntil is null || LeaseUntil <= now), "Refund lease is held.", "PaymentConflict");
        LeaseToken = Guid.CreateVersion7();
        LeaseUntil = until;
        return LeaseToken.Value;
    }

    public void ReleaseLease(Guid token, DateTimeOffset? nextCheckAt)
    {
        PaymentRule.Require(LeaseToken == token, "Refund lease token changed.", "PaymentConflict");
        LeaseToken = null;
        LeaseUntil = null;
        NextCheckAt = nextCheckAt;
    }
}
