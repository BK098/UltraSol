using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.Prices.Events;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Pricing.Domain.Prices;

public sealed partial class SkuPrice : AggregateRoot
{
    private readonly List<PricePeriod> _periods = [];
    private readonly List<PriceChange> _changes = [];
    private DateTimeOffset? _lastMutationAt;

    public Guid PriceListId { get; }
    /// <summary>Stable Catalog ProductItem.Id, independent of the editable SKU code.</summary>
    public Guid SkuId { get; }
    public Currency Currency { get; }
    public PriceListType Type { get; }
    public Guid? ContractId { get; }
    public long Revision { get; private set; }
    public IReadOnlyList<PricePeriod> Periods => _periods.AsReadOnly();
    public IReadOnlyList<PriceChange> Changes => _changes.AsReadOnly();

    private SkuPrice()
    {
        Currency = null!;
    }

    public void RestoreChildren(IEnumerable<PricePeriod> periods, IEnumerable<PriceChange> changes, IEnumerable<ContractPriceAmendment> amendments)
    {
        _periods.Clear();
        _periods.AddRange(periods);
        _changes.Clear();
        _changes.AddRange(changes);
        _amendments.Clear();
        _amendments.AddRange(amendments);
    }

    private SkuPrice(PriceList list, Guid skuId, Guid? contractId)
    {
        PriceListId = list.Id;
        SkuId = skuId;
        Currency = list.Currency;
        Type = list.Type;
        ContractId = contractId;
    }

    public static SkuPrice Create(PriceList list, Guid skuId, Contract? contract = null)
    {
        ArgumentNullException.ThrowIfNull(list);
        Guard.Id(skuId);
        if (list.Type == PriceListType.Contract && (contract is null || contract.PriceListId != list.Id))
        {
            throw new DomainException("A contract price must reference the contract owning its price list.", "ContractMismatch");
        }
        if (list.Type != PriceListType.Contract && contract is not null)
        {
            throw new DomainException("Only contract price lists can reference a contract.", "ContractMismatch");
        }
        return new SkuPrice(list, skuId, contract?.Id);
    }

    public Guid SetInitialPrice(DateTimeOffset effectiveFrom, IEnumerable<PriceTier> tiers, Guid? actorId, DateTimeOffset now, Contract? contract = null)
    {
        EnsureGenericChange(contract, actorId, now);
        if (_periods.Count != 0 || effectiveFrom < now)
        {
            throw new DomainException("Initial price requires an empty timeline and cannot start in the past.", "InvalidInitialPrice");
        }
        var period = NewPeriod(effectiveFrom, tiers);
        Apply([period], PriceChangeKind.SetInitialPrice, actorId, now);
        return period.Id;
    }

    public Guid ChangePriceImmediately(IEnumerable<PriceTier> tiers, Guid? actorId, DateTimeOffset now, Contract? contract = null)
    {
        EnsureGenericChange(contract, actorId, now);
        if (!_periods.Any(period => period.EffectivePeriod.Contains(now)))
        {
            throw new DomainException("An immediate change requires a current price.", "NoApplicablePrice");
        }
        var period = NewPeriod(now, tiers);
        Apply(_periods.Append(period), PriceChangeKind.ChangePriceImmediately, actorId, now);
        return period.Id;
    }

    public Guid SchedulePriceChange(DateTimeOffset effectiveFrom, IEnumerable<PriceTier> tiers, Guid? actorId, DateTimeOffset now, Contract? contract = null)
    {
        EnsureGenericChange(contract, actorId, now);
        if (_periods.Count == 0 || effectiveFrom <= now || effectiveFrom <= _periods[0].EffectivePeriod.From)
        {
            throw new DomainException("Schedule a change after the initial price and in the future.", "InvalidScheduledPrice");
        }
        var period = NewPeriod(effectiveFrom, tiers);
        Apply(_periods.Append(period), PriceChangeKind.SchedulePriceChange, actorId, now);
        return period.Id;
    }

    public void ChangeScheduledPrice(Guid periodId, IEnumerable<PriceTier> tiers, Guid? actorId, DateTimeOffset now, Contract? contract = null)
    {
        EnsureGenericChange(contract, actorId, now);
        var current = RequireFuturePeriod(periodId, now);
        var replacement = new PricePeriod(current.Id, current.EffectivePeriod, PriceTier.Validate(tiers));
        Apply(Replace(replacement), PriceChangeKind.ChangeScheduledPrice, actorId, now);
    }

    public void ReschedulePriceChange(Guid periodId, DateTimeOffset effectiveFrom, Guid? actorId, DateTimeOffset now, Contract? contract = null)
    {
        EnsureGenericChange(contract, actorId, now);
        var current = RequireFuturePeriod(periodId, now);
        RequireFuture(effectiveFrom, now);
        var replacement = new PricePeriod(current.Id, EffectivePeriod.Create(effectiveFrom), current.Tiers);
        Apply(Replace(replacement), PriceChangeKind.ReschedulePriceChange, actorId, now);
    }

    public void CancelScheduledPrice(Guid periodId, Guid? actorId, DateTimeOffset now, Contract? contract = null)
    {
        EnsureGenericChange(contract, actorId, now);
        RequireFuturePeriod(periodId, now);
        Apply(_periods.Where(period => period.Id != periodId), PriceChangeKind.CancelScheduledPrice, actorId, now);
    }

    public ConfiguredPrice? Resolve(int quantity, DateTimeOffset at)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be positive.", "InvalidQuantity");
        }
        var period = _periods.SingleOrDefault(value => value.EffectivePeriod.Contains(at));
        var tier = period?.Tiers.LastOrDefault(value => value.MinimumQuantity <= quantity);
        return tier is null ? null : new ConfiguredPrice(tier.Amount, period!.Id, period.ContractPriceAmendmentId);
    }

    private void EnsureGenericChange(Contract? contract, Guid? actorId, DateTimeOffset now)
    {
        EnsureMutation(actorId, now);
        if (ContractId is not null)
        {
            EnsureContract(contract);
            if (contract!.Status != ContractStatus.Draft)
            {
                throw new DomainException("An activated contract price can only change through an approved amendment.", "ContractAmendmentRequired");
            }
        }
        else if (contract is not null)
        {
            throw new DomainException("This price does not belong to a contract.", "ContractMismatch");
        }
    }

    private void EnsureContract(Contract? contract)
    {
        if (contract is null || contract.Id != ContractId || contract.PriceListId != PriceListId || contract.Currency != Currency)
        {
            throw new DomainException("Contract does not own this price.", "ContractMismatch");
        }
    }

    private void EnsureMutation(Guid? actorId, DateTimeOffset now)
    {
        if (actorId is { } actor)
        {
            Guard.Id(actor);
        }
        if (now < _lastMutationAt)
        {
            throw new DomainException("A price mutation cannot precede the previous mutation.", "InvalidMutationTime");
        }
    }

    private void Touch(Guid? actorId, DateTimeOffset now)
    {
        _lastMutationAt = now.ToUniversalTime();
        MarkUpdated(actorId?.ToString(), now.ToUniversalTime());
    }

    private static void RequireFuture(DateTimeOffset from, DateTimeOffset now)
    {
        if (from <= now)
        {
            throw new DomainException("Only future prices can be scheduled, changed or cancelled.", "InvalidScheduledPrice");
        }
    }

    private PricePeriod RequireFuturePeriod(Guid id, DateTimeOffset now)
    {
        Guard.Id(id);
        var period = _periods.SingleOrDefault(value => value.Id == id) ?? throw EntityNotFoundException.For<PricePeriod>(id);
        RequireFuture(period.EffectivePeriod.From, now);
        return period;
    }

    private static PricePeriod NewPeriod(DateTimeOffset from, IEnumerable<PriceTier> tiers, Guid? amendmentId = null) =>
        new(Guid.NewGuid(), EffectivePeriod.Create(from), PriceTier.Validate(tiers), amendmentId);

    private IEnumerable<PricePeriod> Replace(PricePeriod replacement) => _periods.Select(period => period.Id == replacement.Id ? replacement : period);

    private static PricePeriod[] BuildTimeline(IEnumerable<PricePeriod> proposed)
    {
        var periods = proposed.OrderBy(period => period.EffectivePeriod.From).ToArray();
        if (periods.Select(period => period.EffectivePeriod.From).Distinct().Count() != periods.Length)
        {
            throw new DomainException("Two prices cannot start at the same instant.", "OverlappingPricePeriods");
        }
        for (var index = 0; index < periods.Length; index++)
        {
            periods[index] = periods[index].WithEnd(index + 1 < periods.Length ? periods[index + 1].EffectivePeriod.From : null);
        }
        return periods;
    }

    private void Apply(IEnumerable<PricePeriod> proposed, PriceChangeKind kind, Guid? actorId, DateTimeOffset now, Guid? amendmentId = null)
    {
        var next = BuildTimeline(proposed);
        var previousById = _periods.ToDictionary(period => period.Id);
        var nextById = next.ToDictionary(period => period.Id);
        var before = _periods.Where(period => !nextById.TryGetValue(period.Id, out var other) || !SamePrice(period, other)).ToArray();
        var after = next.Where(period => !previousById.TryGetValue(period.Id, out var other) || !SamePrice(period, other)).ToArray();
        var audit = new PriceChange(kind, actorId, now.ToUniversalTime(), Array.AsReadOnly(before), Array.AsReadOnly(after), amendmentId);
        _periods.Clear();
        _periods.AddRange(next);
        _changes.Add(audit);
        Revision++;
        Touch(actorId, now);
        AddDomainEvent(new SkuPriceTimelineChanged(Id, PriceListId, SkuId, Currency.Code, Type, ContractId, Revision, audit)
        {
            OccurredAt = now.ToUniversalTime()
        });
    }

    private static bool SamePrice(PricePeriod left, PricePeriod right) => left.EffectivePeriod == right.EffectivePeriod &&
        left.ContractPriceAmendmentId == right.ContractPriceAmendmentId && left.Tiers.SequenceEqual(right.Tiers);

    public override void MarkDeleted(string? actorId, DateTimeOffset utcNow)
    {
        throw new DomainException("SKU price history cannot be deleted.");
    }
}