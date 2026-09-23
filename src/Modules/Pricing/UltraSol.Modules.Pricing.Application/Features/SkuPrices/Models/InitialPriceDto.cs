namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;

public sealed record InitialPriceDto(IReadOnlyList<PriceTierDto>? Tiers, DateTimeOffset? EffectiveFrom = null);