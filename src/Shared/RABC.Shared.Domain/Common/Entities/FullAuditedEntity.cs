using UltraSol.Shared.Domain.Common.Abstractions;

namespace UltraSol.Shared.Domain.Common.Entities;

public abstract class FullAuditedEntity<TKey> : SoftDeletableEntity<TKey>, IFullAuditedEntity
    where TKey : notnull
{
    public string ConcurrencyStamp { get; protected set; } = Guid.CreateVersion7().ToString("N");

    protected FullAuditedEntity()
    {
    }

    protected FullAuditedEntity(TKey id)
        : base(id)
    {
    }

    public virtual void RefreshConcurrencyStamp() =>
        ConcurrencyStamp = Guid.CreateVersion7().ToString("N");

    public override void MarkUpdated(string? actorId, DateTimeOffset utcNow)
    {
        base.MarkUpdated(actorId, utcNow);
        RefreshConcurrencyStamp();
    }
}

public abstract class FullAuditedEntity : FullAuditedEntity<Guid>
{
    protected FullAuditedEntity()
    {
    }

    protected FullAuditedEntity(Guid id)
        : base(id)
    {
    }
}
