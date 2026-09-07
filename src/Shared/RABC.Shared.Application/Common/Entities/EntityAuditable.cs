using Domain.Common.Abstractions;

namespace Domain.Common.Entities
{
    public abstract class EntityAuditable<TKey> : BaseEntity, ICreatedUpdatedSoftDeleted
    {
        public string CreatedBy { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public string? UpdatedBy { get; set; }

        public DateTimeOffset? UpdatedAt { get; set; }

        public bool? IsDeleted { get; set; }

        public string? DeletedBy { get; set; }

        public DateTimeOffset? DeletedAt { get; set; }

        public void MarkCreated(string? createdBy, DateTimeOffset createdAt)
        {
            CreatedBy = createdBy ?? string.Empty;
            CreatedAt = createdAt;
        }

        public void MarkUpdated(string? updatedBy, DateTimeOffset updatedAt)
        {
            UpdatedBy = updatedBy;
            UpdatedAt = updatedAt;
        }

        public void MarkDeleted(string? deletedBy, DateTimeOffset deleteAt)
        {
            IsDeleted = true;
            DeletedBy = deletedBy;
            DeletedAt = deleteAt;
        }

        public void Restore()
        {
            IsDeleted = false;
            DeletedBy = null;
            DeletedAt = null;
        }
    }
    public abstract class EntityAuditable : EntityAuditable<Guid>
    {
    }
}