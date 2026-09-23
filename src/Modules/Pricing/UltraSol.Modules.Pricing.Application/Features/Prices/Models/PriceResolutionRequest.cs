using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.ValueObjects;

namespace UltraSol.Modules.Pricing.Application.Features.Prices.Models;

public sealed record PriceResolutionRequest(Guid SkuId, int Quantity, Currency Currency, PriceListType Channel, DateTimeOffset At, Guid? CustomerId = null, Guid? TransactionId = null, Guid? ContractId = null, Guid? NegotiatedPriceId = null);