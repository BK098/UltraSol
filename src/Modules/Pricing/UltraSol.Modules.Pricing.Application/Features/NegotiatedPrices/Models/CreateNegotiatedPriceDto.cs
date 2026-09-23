using UltraSol.Modules.Pricing.Domain.PriceLists;

namespace UltraSol.Modules.Pricing.Application.Features.NegotiatedPrices.Models;

public sealed record CreateNegotiatedPriceDto(Guid CustomerId, Guid SkuId, Guid TransactionId, int Quantity, decimal Amount,
    string? Currency, PriceListType Channel, Guid? ContractId, string? Reason, DateTimeOffset EffectiveFrom, DateTimeOffset EffectiveTo);