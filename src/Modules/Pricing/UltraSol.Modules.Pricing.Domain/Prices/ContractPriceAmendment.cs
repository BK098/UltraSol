using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Entities;

namespace UltraSol.Modules.Pricing.Domain.Prices;

public enum ContractPriceAmendmentStatus
{
    PendingApproval,
    Approved,
    Rejected
}

public sealed class ContractPriceAmendment : BaseEntity
{
    public Guid ContractId { get; }
    public Guid PricePeriodId { get; }
    public PriceChangeKind Kind { get; }
    public DateTimeOffset EffectiveFrom { get; }
    public IReadOnlyList<PriceTier> Tiers { get; }
    public string Reason { get; }
    public long ProposedRevision { get; }
    public Guid? ProposedBy { get; }
    public DateTimeOffset ProposedAt { get; }
    public ContractPriceAmendmentStatus Status { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }

    public ContractPriceAmendment(Guid contractId, Guid pricePeriodId, PriceChangeKind kind, DateTimeOffset effectiveFrom,
        IEnumerable<PriceTier> tiers, string reason, long proposedRevision, Guid? actorId, DateTimeOffset now)
    {
        ContractId = contractId;
        PricePeriodId = pricePeriodId;
        Kind = kind;
        EffectiveFrom = effectiveFrom.ToUniversalTime();
        Tiers = Array.AsReadOnly(tiers.ToArray());
        Reason = reason;
        ProposedRevision = proposedRevision;
        ProposedBy = actorId;
        ProposedAt = now.ToUniversalTime();
    }

    internal void Decide(ContractPriceAmendmentStatus status, Guid? actorId, DateTimeOffset now)
    {
        Status = status;
        DecidedBy = actorId;
        DecidedAt = now.ToUniversalTime();
    }

    public void RestoreDecision(Guid id, ContractPriceAmendmentStatus status, Guid? actorId, DateTimeOffset? at)
    {
        Id = id;
        Status = status;
        DecidedBy = actorId;
        DecidedAt = at;
    }
}