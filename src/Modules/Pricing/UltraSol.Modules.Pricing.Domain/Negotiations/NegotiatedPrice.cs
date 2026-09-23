using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Negotiations.Events;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Pricing.Domain.Negotiations;

public enum NegotiatedPriceStatus
{
    Draft,
    PendingApproval,
    Approved,
    Rejected,
    Revoked
}

public sealed record NegotiatedPriceContext(Guid SkuId, Guid CustomerId, Guid TransactionId, int Quantity, Currency Currency, PriceListType Channel, DateTimeOffset At, Guid? ContractId);

public sealed class NegotiatedPrice : AggregateRoot
{
    public Guid CustomerId { get; }
    public Guid SkuId { get; }
    public Guid TransactionId { get; }
    public int Quantity { get; }
    public decimal Amount { get; }
    public Currency Currency { get; }
    public PriceListType Channel { get; }
    public Guid? ContractId { get; }
    public string Reason { get; }
    public EffectivePeriod Validity { get; }
    public Guid? ProposedBy { get; }
    public DateTimeOffset ProposedAt { get; }
    public NegotiatedPriceStatus Status { get; private set; }
    public Guid? SubmittedBy { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public Guid? RejectedBy { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }
    public Guid? RevokedBy { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private NegotiatedPrice()
    {
        Currency = null!;
        Validity = null!;
        Reason = null!;
    }

    private NegotiatedPrice(Guid customerId, Guid skuId, Guid transactionId, int quantity, decimal amount, Currency currency, PriceListType channel, Guid? contractId, string reason, EffectivePeriod validity, Guid? createdBy, DateTimeOffset createdAt)
    {
        CustomerId = customerId;
        SkuId = skuId;
        TransactionId = transactionId;
        Quantity = quantity;
        Amount = amount;
        Currency = currency;
        Channel = channel;
        ContractId = contractId;
        Reason = reason;
        Validity = validity;
        ProposedBy = createdBy;
        ProposedAt = createdAt;
        MarkCreated(createdBy?.ToString(), createdAt);
    }

    public static NegotiatedPrice Create(Guid customerId, Guid skuId, Guid transactionId, int quantity, decimal amount, Currency currency, PriceListType channel, Guid? contractId, string reason, EffectivePeriod validity, Guid? createdBy, DateTimeOffset now)
    {
        Guard.Id(customerId);
        Guard.Id(skuId);
        Guard.Id(transactionId);
        if (createdBy is { } actor)
        {
            Guard.Id(actor);
        }
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentNullException.ThrowIfNull(validity);
        EnsureTimestamp(now);
        if (quantity <= 0)
        {
            throw new DomainException("Negotiated quantity must be positive.", "InvalidNegotiatedQuantity");
        }
        if (amount < 0)
        {
            throw new DomainException("Negotiated amount cannot be negative.", "InvalidNegotiatedAmount");
        }
        if (channel is not PriceListType.Wholesale and not PriceListType.Contract)
        {
            throw new DomainException("Negotiated prices require Wholesale or Contract channel.", "InvalidNegotiatedChannel");
        }
        if (channel == PriceListType.Contract && contractId is null)
        {
            throw new DomainException("Contract prices require a contract ID.", "ContractIdRequired");
        }
        if (channel == PriceListType.Wholesale && contractId is not null)
        {
            throw new DomainException("Wholesale prices cannot reference a contract.", "UnexpectedContractId");
        }
        if (contractId is not null)
        {
            Guard.Id(contractId.Value);
        }
        if (validity.To is null)
        {
            throw new DomainException("Negotiated prices require a finite validity period.", "FiniteValidityRequired");
        }
        if (validity.To <= now)
        {
            throw new DomainException("Expired validity cannot be negotiated.", "NegotiatedPriceExpired");
        }
        return new NegotiatedPrice(customerId, skuId, transactionId, quantity, amount, currency, channel, contractId, Guard.Required(reason, nameof(Reason)), validity, createdBy, now.ToUniversalTime());
    }

    public void Submit(Guid? actorId, DateTimeOffset now)
    {
        EnsureTransition(actorId, now, NegotiatedPriceStatus.Draft, "submitted");
        Status = NegotiatedPriceStatus.PendingApproval;
        SubmittedBy = actorId;
        SubmittedAt = now.ToUniversalTime();
        RefreshConcurrencyStamp();
        AddDomainEvent(new NegotiatedPriceStatusChanged(Id, CustomerId, TransactionId, SkuId, ContractId, Status) { OccurredAt = now.ToUniversalTime() });
    }

    public void Approve(Guid? actorId, DateTimeOffset now)
    {
        EnsureTransition(actorId, now, NegotiatedPriceStatus.PendingApproval, "approved");
        Status = NegotiatedPriceStatus.Approved;
        ApprovedBy = actorId;
        ApprovedAt = now.ToUniversalTime();
        RefreshConcurrencyStamp();
        AddDomainEvent(new NegotiatedPriceStatusChanged(Id, CustomerId, TransactionId, SkuId, ContractId, Status) { OccurredAt = now.ToUniversalTime() });
    }

    public void Reject(Guid? actorId, DateTimeOffset now)
    {
        EnsureTransition(actorId, now, NegotiatedPriceStatus.PendingApproval, "rejected");
        Status = NegotiatedPriceStatus.Rejected;
        RejectedBy = actorId;
        RejectedAt = now.ToUniversalTime();
        RefreshConcurrencyStamp();
        AddDomainEvent(new NegotiatedPriceStatusChanged(Id, CustomerId, TransactionId, SkuId, ContractId, Status) { OccurredAt = now.ToUniversalTime() });
    }

    public void Revoke(Guid? actorId, DateTimeOffset now)
    {
        EnsureTransition(actorId, now, NegotiatedPriceStatus.Approved, "revoked");
        Status = NegotiatedPriceStatus.Revoked;
        RevokedBy = actorId;
        RevokedAt = now.ToUniversalTime();
        RefreshConcurrencyStamp();
        AddDomainEvent(new NegotiatedPriceStatusChanged(Id, CustomerId, TransactionId, SkuId, ContractId, Status) { OccurredAt = now.ToUniversalTime() });
    }

    public bool IsEffectiveAt(DateTimeOffset at) =>
        ApprovedAt <= at && Validity.Contains(at) && (RevokedAt is null || at < RevokedAt);

    public void EnsureApplicable(NegotiatedPriceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Guard.Id(context.SkuId);
        Guard.Id(context.CustomerId);
        Guard.Id(context.TransactionId);
        if (context.Quantity <= 0)
        {
            throw new DomainException("Request quantity must be positive.", "InvalidNegotiatedQuantity");
        }
        ArgumentNullException.ThrowIfNull(context.Currency);
        EnsureTimestamp(context.At);
        if (Status is not NegotiatedPriceStatus.Approved and not NegotiatedPriceStatus.Revoked || !IsEffectiveAt(context.At))
        {
            throw new DomainException("Negotiated price is not effective at the requested time.", "NegotiatedPriceNotEffective");
        }
        if (context.SkuId != SkuId)
        {
            throw new DomainException("SKU does not match the negotiated price.", "NegotiatedSkuMismatch");
        }
        if (context.CustomerId != CustomerId)
        {
            throw new DomainException("Customer does not match the negotiated price.", "NegotiatedCustomerMismatch");
        }
        if (context.TransactionId != TransactionId)
        {
            throw new DomainException("Transaction does not match the negotiated price.", "NegotiatedTransactionMismatch");
        }
        if (context.Quantity != Quantity)
        {
            throw new DomainException("Quantity does not match the negotiated price.", "NegotiatedQuantityMismatch");
        }
        if (context.Currency != Currency)
        {
            throw new DomainException("Currency does not match the negotiated price.", "NegotiatedCurrencyMismatch");
        }
        if (context.Channel != Channel)
        {
            throw new DomainException("Channel does not match the negotiated price.", "NegotiatedChannelMismatch");
        }
        if (context.ContractId != ContractId)
        {
            throw new DomainException("Contract does not match the negotiated price.", "NegotiatedContractMismatch");
        }
    }

    public override void MarkDeleted(string? actorId, DateTimeOffset utcNow)
    {
        throw new DomainException("Negotiated prices cannot be soft-deleted; revoke approved prices instead.", "NegotiatedPriceDeletionForbidden");
    }

    private void EnsureTransition(Guid? actorId, DateTimeOffset now, NegotiatedPriceStatus expected, string action)
    {
        if (actorId is { } actor)
        {
            Guard.Id(actor);
        }
        EnsureTimestamp(now);
        if (Status != expected)
        {
            throw new DomainException($"Negotiated price cannot be {action} from its current status.", "InvalidNegotiatedPriceTransition");
        }
        if (Validity.To <= now)
        {
            throw new DomainException("Expired negotiated prices cannot transition.", "NegotiatedPriceExpired");
        }
        var previousAt = ApprovedAt ?? SubmittedAt ?? ProposedAt;
        if (now < previousAt)
        {
            throw new DomainException("Transition cannot precede the previous action.", "InvalidNegotiatedPriceTimestamp");
        }
    }

    private static void EnsureTimestamp(DateTimeOffset value)
    {
        if (value == default)
        {
            throw new DomainException("Timestamp is required.", "InvalidTimestamp");
        }
    }
}