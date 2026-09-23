using UltraSol.Modules.Pricing.Domain.Prices;

namespace UltraSol.Modules.Pricing.Infrastructure.Persistence;

public sealed class PricePeriodRow
{
    public Guid Id { get; set; }
    public Guid SkuPriceId { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public Guid? ContractPriceAmendmentId { get; set; }
}

public sealed class PriceTierRow
{
    public Guid PricePeriodId { get; set; }
    public int MinimumQuantity { get; set; }
    public decimal Amount { get; set; }
}

public sealed class PriceChangeRow
{
    public Guid SkuPriceId { get; set; }
    public long Revision { get; set; }
    public PriceChangeKind Kind { get; set; }
    public Guid? ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string BeforeJson { get; set; } = "[]";
    public string AfterJson { get; set; } = "[]";
    public Guid? ContractPriceAmendmentId { get; set; }
}

public sealed class ContractPriceAmendmentRow
{
    public Guid Id { get; set; }
    public Guid SkuPriceId { get; set; }
    public Guid ContractId { get; set; }
    public ContractPriceAmendmentStatus Status { get; set; }
    public DateTimeOffset ProposedAt { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string ProposalJson { get; set; } = null!;
}