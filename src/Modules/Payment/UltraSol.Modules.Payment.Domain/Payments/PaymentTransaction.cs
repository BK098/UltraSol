using UltraSol.Shared.Domain.Common.Entities;

namespace UltraSol.Modules.Payment.Domain.Payments;

public enum PaymentTransactionStatus { Pending, Succeeded, Failed, NeedsReview }

public sealed class PaymentTransaction : BaseEntity
{
    private PaymentTransaction() { }
    internal PaymentTransaction(Guid paymentId, string method, decimal amount, string currency, string key, string fingerprint,
        string source, string? reference, string? providerReference, Guid? actorId, string? note, DateTimeOffset now, DateTimeOffset? expiresAt)
    {
        Id = Guid.CreateVersion7();
        PaymentId = paymentId;
        Method = PaymentRule.Text(method, 32, "Method");
        PaymentRule.Require(Method is "Cash" or "BankTransfer" or "COD" or "Vnpay", "Unsupported payment method.");
        Amount = PaymentRule.Amount(amount);
        Currency = PaymentRule.Currency(currency);
        IdempotencyKey = PaymentRule.Text(key, 200, "IdempotencyKey");
        RequestFingerprint = PaymentRule.Text(fingerprint, 128, "RequestFingerprint");
        Source = PaymentRule.Text(source, 100, "Source");
        Reference = reference is null ? null : PaymentRule.Text(reference, 200, "Reference");
        ProviderReference = providerReference is null ? null : PaymentRule.Text(providerReference, 200, "ProviderReference");
        PaymentRule.Require(Method is not ("BankTransfer" or "COD") || Reference is not null, "Manual receipt requires a reference.");
        ActorId = actorId;
        Note = note;
        CreatedAt = now;
        ExpiresAt = expiresAt;
    }

    public Guid PaymentId { get; private set; }
    public Guid OrderId => PaymentId;
    public string Method { get; private set; } = "";
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "";
    public string IdempotencyKey { get; private set; } = "";
    public string RequestFingerprint { get; private set; } = "";
    public string Source { get; private set; } = "";
    public string? Reference { get; private set; }
    public string? ProviderReference { get; private set; }
    public string? ProviderTransactionNo { get; private set; }
    public PaymentTransactionStatus Status { get; private set; }
    public bool Applied { get; private set; }
    public DateTimeOffset? ReceivedAt { get; private set; }
    public Guid? ActorId { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? GatewayCreatedAt { get; private set; }
    public string? ClientIp { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public DateTimeOffset? NextCheckAt { get; private set; }
    public bool NeedsReconciliation { get; private set; }
    public string? ReviewReason { get; private set; }
    public string? EvidenceReference { get; private set; }
    public Guid? ReconciledBy { get; private set; }
    public DateTimeOffset? ReconciledAt { get; private set; }

    internal void Succeed(string? providerReference, string? providerTransactionNo, DateTimeOffset receivedAt)
    {
        if (Status == PaymentTransactionStatus.Succeeded)
        {
            if ((providerReference is not null && ProviderReference != providerReference) ||
                (providerTransactionNo is not null && ProviderTransactionNo != providerTransactionNo))
            {
                NeedsReconciliation = true;
                ReviewReason = "Contradictory provider success";
            }
            return;
        }
        if (ProviderReference is not null && providerReference is not null)
        {
            if (ProviderReference != providerReference)
            {
                NeedsReview(receivedAt);
                NeedsReconciliation = true;
                ReviewReason = "Provider reference changed";
                return;
            }
        }
        ProviderReference ??= providerReference;
        ProviderTransactionNo = providerTransactionNo;
        ReceivedAt = receivedAt;
        Status = PaymentTransactionStatus.Succeeded;
        LeaseToken = null;
        LeaseUntil = null;
        NextCheckAt = null;
    }

    internal void Fail(DateTimeOffset nextCheckAt)
    {
        PaymentRule.Require(Status != PaymentTransactionStatus.Succeeded, "Successful receipt cannot fail.", "PaymentConflict");
        Status = PaymentTransactionStatus.Failed;
        NextCheckAt = nextCheckAt;
        LeaseToken = null;
        LeaseUntil = null;
    }

    internal void NeedsReview(DateTimeOffset nextCheckAt)
    {
        if (Status == PaymentTransactionStatus.Succeeded)
        {
            NeedsReconciliation = true;
            ReviewReason = "Provider outcome requires review";
            return;
        }
        Status = PaymentTransactionStatus.NeedsReview;
        NextCheckAt = nextCheckAt;
        LeaseToken = null;
        LeaseUntil = null;
    }

    internal void Reconcile(bool received, string evidence, Guid actorId, DateTimeOffset receivedAt, string? providerTransactionNo,
        DateTimeOffset now)
    {
        PaymentRule.Require(Status != PaymentTransactionStatus.Succeeded || received,
            "Successful receipt cannot be reversed.", "PaymentConflict");
        EvidenceReference = PaymentRule.Text(evidence, 500, "EvidenceReference");
        ReconciledBy = actorId;
        ReconciledAt = now;
        NeedsReconciliation = false;
        ReviewReason = null;
        if (received)
        {
            ProviderTransactionNo ??= providerTransactionNo;
            ReceivedAt ??= receivedAt;
            Status = PaymentTransactionStatus.Succeeded;
            LeaseToken = null;
            LeaseUntil = null;
            NextCheckAt = null;
        }
        else
        {
            Status = PaymentTransactionStatus.Failed;
            LeaseToken = null;
            LeaseUntil = null;
            NextCheckAt = null;
        }
    }

    internal void Apply() => Applied = true;

    public Guid ClaimLease(DateTimeOffset now, DateTimeOffset until)
    {
        PaymentRule.Require(until > now && (LeaseUntil is null || LeaseUntil <= now), "Transaction lease is held.", "PaymentConflict");
        LeaseToken = Guid.CreateVersion7();
        LeaseUntil = until;
        return LeaseToken.Value;
    }

    public void ReleaseLease(Guid token, DateTimeOffset? nextCheckAt)
    {
        PaymentRule.Require(LeaseToken == token, "Transaction lease token changed.", "PaymentConflict");
        LeaseToken = null;
        LeaseUntil = null;
        NextCheckAt = nextCheckAt;
    }

    public void MarkGatewayCreated(DateTimeOffset gatewayCreatedAt, string clientIp)
    {
        clientIp = PaymentRule.Text(clientIp, 45, "ClientIp");
        PaymentRule.Require((GatewayCreatedAt is null || GatewayCreatedAt == gatewayCreatedAt) &&
            (ClientIp is null || ClientIp == clientIp), "Gateway request changed.", "PaymentConflict");
        GatewayCreatedAt = gatewayCreatedAt;
        ClientIp = clientIp;
    }
}
