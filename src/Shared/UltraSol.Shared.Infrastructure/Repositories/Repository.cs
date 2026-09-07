using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Delegates;
using UltraSol.Shared.Domain.Common.Repositories;

namespace UltraSol.Shared.Infrastructure.Repositories;

public class Repository<TEntity, TKey>(DbContext context)
    : QueryRepository<TEntity, TKey>(context), IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull
{
    private readonly CommandRepository<TEntity, TKey> _commands = new(context);

    public Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        _commands.AddAsync(entity, cancellationToken);

    public Task<TEntity> AddAsync(
        TEntity entity,
        EntityMutator<TEntity> beforeSave,
        CancellationToken cancellationToken = default) =>
        _commands.AddAsync(entity, beforeSave, cancellationToken);

    public Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default) =>
        _commands.AddRangeAsync(entities, cancellationToken);

    public Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        _commands.UpdateAsync(entity, cancellationToken);

    public Task UpdateAsync(
        TEntity entity,
        EntityMutator<TEntity> mutate,
        CancellationToken cancellationToken = default) =>
        _commands.UpdateAsync(entity, mutate, cancellationToken);

    public Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default) =>
        _commands.UpdateRangeAsync(entities, cancellationToken);

    public Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        _commands.DeleteAsync(entity, cancellationToken);

    public Task DeleteAsync(TKey id, CancellationToken cancellationToken = default) =>
        _commands.DeleteAsync(id, cancellationToken);

    public Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default) =>
        _commands.DeleteRangeAsync(entities, cancellationToken);

    public Task DeleteWhereAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        _commands.DeleteWhereAsync(predicate, cancellationToken);

    public Task SoftDeleteAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        _commands.SoftDeleteAsync(entity, cancellationToken);

    public Task SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default) =>
        _commands.SoftDeleteAsync(id, cancellationToken);

    public Task RestoreAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        _commands.RestoreAsync(entity, cancellationToken);

    public Task RestoreAsync(TKey id, CancellationToken cancellationToken = default) =>
        _commands.RestoreAsync(id, cancellationToken);

    public Task<TEntity> GetOrAddAsync(
        TKey id,
        EntityFactory<TEntity> factory,
        CancellationToken cancellationToken = default) =>
        _commands.GetOrAddAsync(id, factory, cancellationToken);

    public Task<TEntity?> FindTrackedAsync(TKey id, CancellationToken cancellationToken = default) =>
        _commands.FindTrackedAsync(id, cancellationToken);

    public Task<TEntity> GetTrackedRequiredAsync(TKey id, CancellationToken cancellationToken = default) =>
        _commands.GetTrackedRequiredAsync(id, cancellationToken);

    public Task<int> ExecuteUpdateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Action<IEntityUpdater<TEntity>> update,
        CancellationToken cancellationToken = default) =>
        _commands.ExecuteUpdateAsync(predicate, update, cancellationToken);

    public Task<int> ExecuteDeleteAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        _commands.ExecuteDeleteAsync(predicate, cancellationToken);
}

public class Repository<TEntity>(DbContext context)
    : Repository<TEntity, Guid>(context), IRepository<TEntity>
    where TEntity : class, IEntity<Guid>;
