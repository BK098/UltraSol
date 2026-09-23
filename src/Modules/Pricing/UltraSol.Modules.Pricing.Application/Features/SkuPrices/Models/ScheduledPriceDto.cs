namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;

public sealed record ScheduledPriceDto(DateTimeOffset EffectiveFrom, IReadOnlyList<PriceTierDto>? Tiers);