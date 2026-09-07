namespace UltraSol.Shared.Domain.Common.Abstractions;

public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void AddDomainEvent(IDomainEvent domainEvent);
    void RemoveDomainEvent(IDomainEvent domainEvent);
    void ClearDomainEvents();
}

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IAuditActorAccessor
{
    string? GetActorId();
    string? GetActorName();
    bool IsAuthenticated { get; }
}