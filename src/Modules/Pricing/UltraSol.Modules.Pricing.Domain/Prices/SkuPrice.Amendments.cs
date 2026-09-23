using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Pricing.Domain.Prices;

public sealed partial class SkuPrice
{
    private readonly List<ContractPriceAmendment> _amendments = [];

    public IReadOnlyList<ContractPriceAmendment> ContractPriceAmendments => _amendments.AsReadOnly();

    public Guid ProposeContractPriceChange(Contract contract, DateTimeOffset effectiveFrom, IEnumerable<PriceTier> tiers,
        string reason, Guid? actorId, DateTimeOffset now)
    {
        var kind = _periods.Count == 0 ? PriceChangeKind.SetInitialPrice :
            effectiveFrom == now ? PriceChangeKind.ChangePriceImmediately : PriceChangeKind.SchedulePriceChange;
        return Propose(contract, Guid.NewGuid(), kind, effectiveFrom, PriceTier.Validate(tiers), reason, actorId, now);
    }

    public Guid ProposeContractScheduledPriceChange(Contract contract, Guid periodId, IEnumerable<PriceTier> tiers,
        string reason, Guid? actorId, DateTimeOffset now)
    {
        var period = RequireFuturePeriod(periodId, now);
        return Propose(contract, period.Id, PriceChangeKind.ChangeScheduledPrice, period.EffectivePeriod.From, PriceTier.Validate(tiers), reason, actorId, now);
    }

    public Guid ProposeContractPriceReschedule(Contract contract, Guid periodId, DateTimeOffset effectiveFrom, string reason, Guid? actorId, DateTimeOffset now)
    {
        var period = RequireFuturePeriod(periodId, now);
        RequireFuture(effectiveFrom, now);
        return Propose(contract, period.Id, PriceChangeKind.ReschedulePriceChange, effectiveFrom, period.Tiers, reason, actorId, now);
    }

    public Guid ProposeContractPriceCancellation(Contract contract, Guid periodId, string reason, Guid? actorId, DateTimeOffset now)
    {
        var period = RequireFuturePeriod(periodId, now);
        return Propose(contract, period.Id, PriceChangeKind.CancelScheduledPrice, period.EffectivePeriod.From, period.Tiers, reason, actorId, now);
    }

    public void ApproveContractPriceAmendment(Contract contract, Guid amendmentId, Guid? actorId, DateTimeOffset now)
    {
        EnsureMutation(actorId, now);
        EnsureContract(contract);
        var amendment = RequirePendingAmendment(amendmentId, now);
        contract.EnsureCanAmend(PriceListId, amendment.EffectiveFrom, now);
        if (amendment.ProposedRevision != Revision)
        {
            throw new DomainException("Price timeline changed after this amendment was proposed; submit a new proposal.", "StalePriceAmendment");
        }
        var next = AmendmentTimeline(amendment, now);
        Apply(next, amendment.Kind, actorId, now, amendment.Id);
        amendment.Decide(ContractPriceAmendmentStatus.Approved, actorId, now);
    }

    public void RejectContractPriceAmendment(Guid amendmentId, Guid? actorId, DateTimeOffset now)
    {
        EnsureMutation(actorId, now);
        var amendment = RequirePendingAmendment(amendmentId, now);
        amendment.Decide(ContractPriceAmendmentStatus.Rejected, actorId, now);
        Touch(actorId, now);
    }

    private Guid Propose(Contract contract, Guid periodId, PriceChangeKind kind, DateTimeOffset effectiveFrom,
        IEnumerable<PriceTier> tiers, string reason, Guid? actorId, DateTimeOffset now)
    {
        EnsureMutation(actorId, now);
        EnsureContract(contract);
        contract.EnsureCanAmend(PriceListId, effectiveFrom, now);
        var amendment = new ContractPriceAmendment(contract.Id, periodId, kind, effectiveFrom, tiers,
            Guard.Required(reason, nameof(reason)), Revision, actorId, now);
        AmendmentTimeline(amendment, now);
        _amendments.Add(amendment);
        Touch(actorId, now);
        return amendment.Id;
    }

    private ContractPriceAmendment RequirePendingAmendment(Guid id, DateTimeOffset now)
    {
        Guard.Id(id);
        var amendment = _amendments.SingleOrDefault(value => value.Id == id) ?? throw EntityNotFoundException.For<ContractPriceAmendment>(id);
        if (amendment.Status != ContractPriceAmendmentStatus.PendingApproval || now < amendment.ProposedAt)
        {
            throw new DomainException("Only pending amendments can be decided, after their proposal time.", "InvalidAmendmentTransition");
        }
        return amendment;
    }

    private PricePeriod[] AmendmentTimeline(ContractPriceAmendment amendment, DateTimeOffset now)
    {
        var replacement = new PricePeriod(amendment.PricePeriodId, EffectivePeriod.Create(amendment.EffectiveFrom), amendment.Tiers, amendment.Id);
        switch (amendment.Kind)
        {
            case PriceChangeKind.SetInitialPrice:
            case PriceChangeKind.ChangePriceImmediately:
            case PriceChangeKind.SchedulePriceChange:
                return BuildTimeline(_periods.Append(replacement));
            case PriceChangeKind.ChangeScheduledPrice:
            case PriceChangeKind.ReschedulePriceChange:
                RequireFuturePeriod(amendment.PricePeriodId, now);
                RequireFuture(amendment.EffectiveFrom, now);
                return BuildTimeline(Replace(replacement));
            case PriceChangeKind.CancelScheduledPrice:
                RequireFuturePeriod(amendment.PricePeriodId, now);
                return BuildTimeline(_periods.Where(period => period.Id != amendment.PricePeriodId));
            default:
                throw new DomainException("Unsupported amendment operation.", "InvalidAmendmentOperation");
        }
    }
}