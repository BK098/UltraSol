using UltraSol.Shared.Domain.Common.Events;

namespace UltraSol.Modules.Pricing.Domain.Contracts.Events;

public sealed record ContractStatusChanged(Guid ContractId, Guid CustomerId, Guid PriceListId, ContractStatus Status) : DomainEvent;