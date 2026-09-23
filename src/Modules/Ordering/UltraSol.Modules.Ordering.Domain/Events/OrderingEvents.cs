using UltraSol.Shared.Domain.Common.Events;

namespace UltraSol.Modules.Ordering.Domain.Events;

public sealed record CartCheckedOut(Guid CartId) : DomainEvent;
public sealed record OrderPlaced(Guid OrderId, long OrderVersion) : DomainEvent;
public sealed record OrderConfirmed(Guid OrderId, long OrderVersion) : DomainEvent;
public sealed record OrderRejected(Guid OrderId, long OrderVersion, string Reason) : DomainEvent;
public sealed record OrderCancelled(Guid OrderId, long OrderVersion, string Reason) : DomainEvent;
public sealed record OrderCompleted(Guid OrderId, long OrderVersion) : DomainEvent;