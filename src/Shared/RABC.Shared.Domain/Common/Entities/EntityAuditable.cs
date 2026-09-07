using UltraSol.Shared.Domain.Common.Abstractions;

namespace UltraSol.Shared.Domain.Common.Entities
{
    public abstract class EntityAuditable<TKey> : BaseEntity<TKey>,  ICreatedUpdated where TKey : notnull
    {
        protected EntityAuditable()
        {
        }
        protected EntityAuditable(TKey id): base(id)
        {
        }

        public string? CreatedBy { get; protected set; }
        public string? UpdatedBy { get; protected set; }
        public DateTimeOffset CreatedAt { get;  set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public void MarkCreated(string? createdBy, DateTimeOffset createdAt)
        {
            if (CreatedAt != default)
            {
                return;
            }
            CreatedBy = createdBy;
            CreatedAt = createdAt;
        }

        public virtual void MarkUpdated(string? updatedBy, DateTimeOffset updatedAt)
        {
            UpdatedBy = updatedBy;
            UpdatedAt = updatedAt;
        }
    }
    public abstract class EntityAuditable : EntityAuditable<Guid>;
}