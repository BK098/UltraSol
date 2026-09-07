using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Delegates;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Repositories;

namespace UltraSol.Shared.Infrastructure.Repositories;

public class CommandRepository<TEntity, TKey>(DbContext context) : ICommandRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull
{
    protected DbContext Context { get; } = context;
    protected DbSet<TEntity> Set => Context.Set<TEntity>();

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await Set.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return entity;
    }

    public virtual async Task<TEntity> AddAsync(
        TEntity entity,
        EntityMutator<TEntity> beforeSave,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(beforeSave);
        beforeSave(entity);
        return await AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public virtual Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        return Set.AddRangeAsync(entities, cancellationToken);
    }

    public virtual Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Set.Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task UpdateAsync(
        TEntity entity,
        EntityMutator<TEntity> mutate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        mutate(entity);
        return UpdateAsync(entity, cancellationToken);
    }

    public virtual Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        Set.UpdateRange(entities);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        (Context as IRepositoryWritePolicy)?.EnsureDeleteAllowed(typeof(TEntity));
        ArgumentNullException.ThrowIfNull(entity);
        Set.Remove(entity);
        return Task.CompletedTask;
    }

    public virtual async Task DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await FindTrackedAsync(id, cancellationToken).ConfigureAwait(false)
                     ?? throw EntityNotFoundException.For<TEntity>(id);
        await DeleteAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public virtual Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        (Context as IRepositoryWritePolicy)?.EnsureDeleteAllowed(typeof(TEntity));
        ArgumentNullException.ThrowIfNull(entities);
        Set.RemoveRange(entities);
        return Task.CompletedTask;
    }

    public virtual Task DeleteWhereAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        (Context as IRepositoryWritePolicy)?.EnsureDeleteAllowed(typeof(TEntity));
        return Set.Where(predicate).ExecuteDeleteAsync(cancellationToken);
    }

    public virtual Task SoftDeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity is not ISoftDeleted deletable)
        {
            throw new InvalidOperationException($"{typeof(TEntity).Name} does not support soft delete.");
        }

        deletable.MarkDeleted(null, DateTimeOffset.UtcNow);
        Set.Update(entity);
        return Task.CompletedTask;
    }

    public virtual async Task SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await FindTrackedAsync(id, cancellationToken).ConfigureAwait(false)
                     ?? throw EntityNotFoundException.For<TEntity>(id);
        await SoftDeleteAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public virtual Task RestoreAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity is not ISoftDeleted deletable)
        {
            throw new InvalidOperationException($"{typeof(TEntity).Name} does not support restore.");
        }

        deletable.Restore();
        Set.Update(entity);
        return Task.CompletedTask;
    }

    public virtual async Task RestoreAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await Set.IgnoreQueryFilters()
            .FirstOrDefaultAsync(EntityPredicates.ById<TEntity, TKey>(id), cancellationToken)
            .ConfigureAwait(false)
            ?? throw EntityNotFoundException.For<TEntity>(id);

        await RestoreAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<TEntity> GetOrAddAsync(
        TKey id,
        EntityFactory<TEntity> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var existing = await FindTrackedAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        return await AddAsync(factory(), cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<TEntity> GetTrackedRequiredAsync(TKey id, CancellationToken cancellationToken = default) =>
        await FindTrackedAsync(id, cancellationToken).ConfigureAwait(false)
        ?? throw EntityNotFoundException.For<TEntity>(id);

    public virtual Task<int> ExecuteUpdateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Action<IEntityUpdater<TEntity>> update,
        CancellationToken cancellationToken = default)
    {
        (Context as IRepositoryWritePolicy)?.EnsureBulkWriteAllowed(typeof(TEntity));
        ArgumentNullException.ThrowIfNull(update);
        var updater = new EntityUpdater<TEntity>();
        update(updater);
        return updater.ApplyAsync(Set.Where(predicate), cancellationToken);
    }

    public virtual Task<int> ExecuteDeleteAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        (Context as IRepositoryWritePolicy)?.EnsureDeleteAllowed(typeof(TEntity));
        return Set.Where(predicate).ExecuteDeleteAsync(cancellationToken);
    }

    public virtual async Task<TEntity?> FindTrackedAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var local = Set.Local.FirstOrDefault(entity => EqualityComparer<TKey>.Default.Equals(entity.Id, id));
        return await Context.OneAsync(local is not null ? Task.FromResult<TEntity?>(local) : Set.FirstOrDefaultAsync(
            EntityPredicates.ById<TEntity, TKey>(id),
            cancellationToken), cancellationToken).ConfigureAwait(false);
    }
}

public class CommandRepository<TEntity>(DbContext context)
    : CommandRepository<TEntity, Guid>(context), ICommandRepository<TEntity>
    where TEntity : class, IEntity<Guid>;

internal sealed class EntityUpdater<TEntity> : IEntityUpdater<TEntity>
    where TEntity : class
{
    private readonly List<Action<UpdateSettersBuilder<TEntity>>> _setters = [];

    public IEntityUpdater<TEntity> Set<TProperty>(
        Expression<Func<TEntity, TProperty>> property,
        TProperty value)
    {
        _setters.Add(setters => setters.SetProperty(property, value));
        return this;
    }

    public IEntityUpdater<TEntity> Set<TProperty>(
        Expression<Func<TEntity, TProperty>> property,
        Expression<Func<TEntity, TProperty>> valueFactory)
    {
        _setters.Add(setters => setters.SetProperty(property, valueFactory));
        return this;
    }

    public Task<int> ApplyAsync(IQueryable<TEntity> query, CancellationToken cancellationToken)
    {
        if (_setters.Count == 0)
        {
            throw new InvalidOperationException("ExecuteUpdate requires at least one Set(...) call.");
        }

        return query.ExecuteUpdateAsync(setters => _setters.ForEach(apply => apply(setters)), cancellationToken);
    }
}
