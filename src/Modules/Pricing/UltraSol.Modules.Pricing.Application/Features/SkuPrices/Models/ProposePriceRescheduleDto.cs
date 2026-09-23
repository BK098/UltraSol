namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;

public sealed record ProposePriceRescheduleDto(Guid PeriodId, DateTimeOffset EffectiveFrom, string? Reason);