namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;

public sealed record CreateSkuPriceDto(Guid PriceListId, Guid SkuId);