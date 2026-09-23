using UltraSol.Modules.Pricing.Domain.PriceLists;

namespace UltraSol.Modules.Pricing.Application.Features.PriceLists.Models;

public sealed record CreatePriceListDto(string? Name, string? Currency, PriceListType Type);