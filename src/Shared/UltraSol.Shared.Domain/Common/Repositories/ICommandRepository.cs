using System.Linq.Expressions;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Delegates;

namespace UltraSol.Shared.Domain.Common.Repositories;

public interface IEntityUpdater<TEntity>
    where TEntity : class
{
    IEntityUpdater<TEntity> Set<TProperty>(
        Expression<Func<TEntity, TProperty>> property,
        TProperty value);

    IEntityUpdater<TEntity> Set<TProperty>(
        Expression<Func<TEntity, TProperty>> property,
        Expression<Func<TEntity, TProperty>> valueFactory);
}

public interface ICommandRepository<TEntity, TKey>
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
    Task<TEntity> GetOrAddAsync(TKey id,EntityFactory<TEntity> factory,CancellationToken cancellationToken = default);
    Task<TEntity?> FindTrackedAsync(TKey id, CancellationToken cancellationToken = default);
    Task<TEntity> GetTrackedRequiredAsync(TKey id, CancellationToken cancellationToken = default);
    Task<int> ExecuteUpdateAsync(Expression<Func<TEntity, bool>> predicate, Action<IEntityUpdater<TEntity>> update, CancellationToken cancellationToken = default);
    Task<int> ExecuteDeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
}

public interface ICommandRepository<TEntity> : ICommandRepository<TEntity, Guid>
    where TEntity : class, IEntity<Guid>;