namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;

public sealed record ProposeScheduledPriceChangeDto(Guid PeriodId, IReadOnlyList<PriceTierDto>? Tiers, string? Reason);