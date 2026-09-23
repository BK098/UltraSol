using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Contracts.Events;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Pricing.Domain.Contracts;

public enum ContractStatus
{
    Draft,
    Active,
    Terminated
}

public sealed class Contract : AggregateRoot
{
    public Guid CustomerId { get; }
    public Guid PriceListId { get; }
    public Currency Currency { get; }
    public EffectivePeriod Validity { get; }
    public string CommercialTerms { get; }
    public ContractStatus Status { get; private set; }
    public DateTimeOffset? ActivatedAt { get; private set; }
    public Guid? ActivatedBy { get; private set; }
    public DateTimeOffset? TerminatedAt { get; private set; }
    public Guid? TerminatedBy { get; private set; }

    private Contract()
    {
        Currency = null!;
        Validity = null!;
        CommercialTerms = null!;
    }

    private Contract(Guid customerId, PriceList priceList, EffectivePeriod validity, string commercialTerms)
    {
        CustomerId = customerId;
        PriceListId = priceList.Id;
        Currency = priceList.Currency;
        Validity = validity;
        CommercialTerms = commercialTerms;
    }

    public static Contract Create(Guid customerId, PriceList priceList, EffectivePeriod validity, string commercialTerms)
    {
        Guard.Id(customerId);
        ArgumentNullException.ThrowIfNull(priceList);
        ArgumentNullException.ThrowIfNull(validity);
        if (priceList.Type != PriceListType.Contract)
        {
            throw new DomainException("Contracts require a Contract price list.", "InvalidContractPriceList");
        }
        return new Contract(customerId, priceList, validity, Guard.Required(commercialTerms, nameof(CommercialTerms)));
    }

    public void Activate(Guid? actorId, DateTimeOffset now)
    {
        if (actorId is { } actor)
        {
            Guard.Id(actor);
        }
        EnsureTimestamp(now);
        if (Status != ContractStatus.Draft)
        {
            throw new DomainException("Only draft contracts can be activated.", "InvalidContractTransition");
        }
        if (Validity.To <= now)
        {
            throw new DomainException("Expired contracts cannot be activated.", "ContractExpired");
        }
        Status = ContractStatus.Active;
        ActivatedBy = actorId;
        ActivatedAt = now.ToUniversalTime();
        RefreshConcurrencyStamp();
        AddDomainEvent(new ContractStatusChanged(Id, CustomerId, PriceListId, Status) { OccurredAt = now.ToUniversalTime() });
    }

    public void Terminate(Guid? actorId, DateTimeOffset now)
    {
        if (actorId is { } actor)
        {
            Guard.Id(actor);
        }
        EnsureTimestamp(now);
        if (Status != ContractStatus.Active)
        {
            throw new DomainException("Only active contracts can be terminated.", "InvalidContractTransition");
        }
        if (now < ActivatedAt)
        {
            throw new DomainException("Termination cannot precede activation.", "InvalidContractTimestamp");
        }
        Status = ContractStatus.Terminated;
        TerminatedBy = actorId;
        TerminatedAt = now.ToUniversalTime();
        RefreshConcurrencyStamp();
        AddDomainEvent(new ContractStatusChanged(Id, CustomerId, PriceListId, Status) { OccurredAt = now.ToUniversalTime() });
    }

    public bool IsEffectiveAt(DateTimeOffset at) =>
        ActivatedAt <= at && Validity.Contains(at) && (TerminatedAt is null || at < TerminatedAt);

    public void EnsureCanAmend(Guid priceListId, DateTimeOffset effectiveFrom, DateTimeOffset now)
    {
        Guard.Id(priceListId);
        EnsureTimestamp(effectiveFrom);
        EnsureTimestamp(now);
        if (priceListId != PriceListId)
        {
            throw new DomainException("Amendment price list does not belong to this contract.", "ContractPriceListMismatch");
        }
        if (Status != ContractStatus.Active || TerminatedAt is not null)
        {
            throw new DomainException("Only active, unterminated contracts can be amended.", "ContractNotAmendable");
        }
        if (now < ActivatedAt)
        {
            throw new DomainException("Amendment checks cannot precede contract activation.", "InvalidAmendmentDate");
        }
        if (Validity.To <= now)
        {
            throw new DomainException("Expired contracts cannot be amended.", "ContractExpired");
        }
        if (effectiveFrom < now || !Validity.Contains(effectiveFrom))
        {
            throw new DomainException("Amendment date must be current or future and within contract validity.", "InvalidAmendmentDate");
        }
    }

    public override void MarkDeleted(string? actorId, DateTimeOffset utcNow)
    {
        throw new DomainException("Contracts cannot be soft-deleted; terminate them instead.", "ContractDeletionForbidden");
    }

    private static void EnsureTimestamp(DateTimeOffset value)
    {
        if (value == default)
        {
            throw new DomainException("Timestamp is required.", "InvalidTimestamp");
        }
    }
}