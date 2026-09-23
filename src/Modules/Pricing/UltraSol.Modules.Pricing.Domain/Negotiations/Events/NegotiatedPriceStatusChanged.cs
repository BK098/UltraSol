using UltraSol.Shared.Domain.Common.Events;

namespace UltraSol.Modules.Pricing.Domain.Negotiations.Events;

public sealed record NegotiatedPriceStatusChanged(Guid NegotiatedPriceId, Guid CustomerId, Guid TransactionId,
    Guid SkuId, Guid? ContractId, NegotiatedPriceStatus Status) : DomainEvent;