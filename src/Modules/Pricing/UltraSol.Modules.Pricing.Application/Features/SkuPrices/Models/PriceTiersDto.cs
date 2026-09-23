namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;

public sealed record PriceTiersDto(IReadOnlyList<PriceTierDto>? Tiers);