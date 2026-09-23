using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Entities;

namespace UltraSol.Modules.Pricing.Domain.Prices;

public sealed class PricePeriod : BaseEntity
{
    public EffectivePeriod EffectivePeriod { get; }
    public IReadOnlyList<PriceTier> Tiers { get; }
    public Guid? ContractPriceAmendmentId { get; }

    public PricePeriod(Guid id, EffectivePeriod effectivePeriod, IEnumerable<PriceTier> tiers, Guid? amendmentId = null) : base(id)
    {
        EffectivePeriod = effectivePeriod;
        Tiers = Array.AsReadOnly(tiers.ToArray());
        ContractPriceAmendmentId = amendmentId;
    }

    internal PricePeriod WithEnd(DateTimeOffset? end) => new(Id, EffectivePeriod.Create(EffectivePeriod.From, end), Tiers, ContractPriceAmendmentId);
}

public sealed record ConfiguredPrice(decimal Amount, Guid PricePeriodId, Guid? ContractPriceAmendmentId);