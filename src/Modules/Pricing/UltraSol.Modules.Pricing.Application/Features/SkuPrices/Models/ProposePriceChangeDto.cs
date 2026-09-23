namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;

public sealed record ProposePriceChangeDto(DateTimeOffset EffectiveFrom, IReadOnlyList<PriceTierDto>? Tiers, string? Reason);