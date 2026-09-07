using Domain.Common.Abstractions;
using Domain.Common.Delegates;
using System.Linq.Expressions;

namespace Domain.Common.Repositories
{
    public interface IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull
    {
        Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
        Task<TEntity> AddAsync(TEntity entity, EntityMutator<TEntity> beforeSave, CancellationToken cancellationToken = default);
        Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
        Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
        Task UpdateAsync(TEntity entity, EntityMutator<TEntity> mutate, CancellationToken cancellationToken = default);
        Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
        Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
        Task DeleteAsync(TKey id, CancellationToken cancellationToken = default);
        Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
        Task DeleteWhereAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
        Task SoftDeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
        Task SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default);
        Task RestoreAsync(TEntity entity, CancellationToken cancellationToken = default);
        Task RestoreAsync(TKey id, CancellationToken cancellationToken = default);
        Task<TEntity> GetOrAddAsync(TKey id, EntityFactory<TEntity> factory, CancellationToken cancellationToken = default);
        Task<TEntity?> FindTrackedAsync(TKey id, CancellationToken cancellationToken = default);
        Task<TEntity> GetTrackedRequiredAsync(TKey id, CancellationToken cancellationToken = default);
        Task<int> ExecuteDeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    }

    public interface IRepository<TEntity> : IRepository<TEntity, Guid>
    where TEntity : class, IEntity<Guid>;
}