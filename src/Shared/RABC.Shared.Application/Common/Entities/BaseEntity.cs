using Domain.Common.Abstractions;

namespace Domain.Common.Entities
{
    public abstract class BaseEntity : BaseEntity<Guid>
    {
        protected BaseEntity()
        {
        }

        protected BaseEntity(Guid id) : base(id)
        {
        }
    }

    public abstract class BaseEntity<TKey> : IEntity<TKey> where TKey : notnull
    {
        public TKey Id { get; protected set; } = default!;
        protected BaseEntity()
        {
            if (typeof(TKey) == typeof(Guid))
            {
                Id = (TKey)(object)Guid.NewGuid();
            }
        }

        protected BaseEntity(TKey id)
        {
            Id = id;
        }
        public object GetId() => Id;
    }
}
