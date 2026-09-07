using UltraSol.Shared.Domain.Common.Abstractions;

namespace UltraSol.Shared.Domain.Common.Entities;

public abstract class AggregateRoot<TKey> : FullAuditedEntity<TKey>, IAggregateRoot, IHasDomainEvents
    where TKey : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot()
    {
    }

    protected AggregateRoot(TKey id)
        : base(id)
    {
    }

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    public void RemoveDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Remove(domainEvent);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}

public abstract class AggregateRoot : AggregateRoot<Guid>
{
    protected AggregateRoot()
    {
    }

    protected AggregateRoot(Guid id)
        : base(id)
    {
    }
}
