using UltraSol.Shared.Domain.Common.Abstractions;

namespace UltraSol.Shared.Domain.Common.Entities;

public abstract class SoftDeletableEntity<TKey> : EntityAuditable<TKey>, ISoftDeleted
    where TKey : notnull
{
    public bool IsDeleted { get; protected set; }
    public DateTimeOffset? DeletedAt { get; protected set; }
    public string? DeletedBy { get; protected set; }

    protected SoftDeletableEntity()
    {
    }

    protected SoftDeletableEntity(TKey id)
        : base(id)
    {
    }

    public virtual void MarkDeleted(string? actorId, DateTimeOffset utcNow)
    {
        IsDeleted = true;
        DeletedAt = utcNow;
        DeletedBy = actorId;
        MarkUpdated(actorId, utcNow);
    }

    public virtual void Restore()
    {
        if (!IsDeleted)
        {
            return;
        }

        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }
}

public abstract class SoftDeletableEntity : SoftDeletableEntity<Guid>
{
    protected SoftDeletableEntity()
    {
    }

    protected SoftDeletableEntity(Guid id)
        : base(id)
    {
    }
}
