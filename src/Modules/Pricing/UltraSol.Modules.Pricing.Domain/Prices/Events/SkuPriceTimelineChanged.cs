using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Shared.Domain.Common.Events;

namespace UltraSol.Modules.Pricing.Domain.Prices.Events;

public sealed record SkuPriceTimelineChanged(Guid SkuPriceId, Guid PriceListId, Guid SkuId, string Currency,
    PriceListType Channel, Guid? ContractId, long Revision, PriceChange Change) : DomainEvent;