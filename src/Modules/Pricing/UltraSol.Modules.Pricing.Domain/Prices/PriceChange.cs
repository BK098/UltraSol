namespace UltraSol.Modules.Pricing.Domain.Prices;

public enum PriceChangeKind
{
    SetInitialPrice,
    ChangePriceImmediately,
    SchedulePriceChange,
    ChangeScheduledPrice,
    ReschedulePriceChange,
    CancelScheduledPrice
}

public sealed record PriceChange(PriceChangeKind Kind, Guid? ActorId, DateTimeOffset OccurredAt,
    IReadOnlyList<PricePeriod> Before, IReadOnlyList<PricePeriod> After, Guid? ContractPriceAmendmentId);