using UltraSol.Modules.Pricing.Domain.ValueObjects;

namespace UltraSol.Modules.Pricing.Application.Features.Prices.Models;

public sealed record ResolvedPrice(decimal UnitPrice, Currency Currency, Guid SkuId, int Quantity, DateTimeOffset At, Guid PriceListId, Guid? SkuPriceId, Guid? PricePeriodId, Guid? ContractPriceAmendmentId, Guid? NegotiatedPriceId, Guid? ContractId);