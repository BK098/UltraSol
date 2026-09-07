using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Delegates;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Extensions;
using UltraSol.Shared.Domain.Common.Guards;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Domain.Common.Results;
using UltraSol.Shared.Domain.Common.Specifications;

namespace UltraSol.Shared.Domain.Common.Repositories;

public static class QueryRepositoryExtensions
{
    public static Task<TEntity?> FindAsync<TEntity, TKey>(
        this IQueryRepository<TEntity, TKey> repository,
        QueryFilter<TEntity> filter,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull =>
        repository.FirstOrDefaultAsync(filter(), cancellationToken);

    public static Task<IReadOnlyList<TEntity>> ListAsync<TEntity, TKey>(
        this IQueryRepository<TEntity, TKey> repository,
        QueryFilter<TEntity> filter,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull =>
        repository.ListAsync(filter(), cancellationToken);

    public static Task<PaginatedResult<TEntity>> GetPagedAsync<TEntity, TKey>(
        this IQueryRepository<TEntity, TKey> repository,
        PaginationRequest page,
        QueryFilter<TEntity>? filter,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull =>
        repository.GetPagedAsync(page, filter?.Invoke(), cancellationToken);

    public static Task<PaginatedResult<TEntity>> GetPagedAsync<TEntity, TKey>(
        this IQueryRepository<TEntity, TKey> repository,
        PaginationRequest page,
        Action<ISpecificationBuilder<TEntity>> configure,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull =>
        repository.GetPagedAsync(page, Spec.For(configure), cancellationToken);

    public static async Task<Result<TEntity>> GetByIdResultAsync<TEntity, TKey>(
        this IQueryRepository<TEntity, TKey> repository,
        TKey id,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return entity.ToResult(Error.NotFound("entity_not_found", $"{typeof(TEntity).Name} '{id}' was not found."));
    }

    public static Task<TResult> QueryAsync<TEntity, TKey, TResult>(
        this IQueryRepository<TEntity, TKey> repository,
        QueryPipeline<TEntity> pipeline,
        Func<IQueryable<TEntity>, CancellationToken, Task<TResult>> materialize,
        bool tracking = false,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull =>
        repository.ExecuteAsync((query, token) => materialize(pipeline(query), token), tracking, cancellationToken);

    public static Task<bool> ExistsAsync<TEntity, TKey>(
        this IQueryRepository<TEntity, TKey> repository,
        TKey id,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull =>
        repository.AnyAsync(ExpressionExtensions.Equal<TEntity, TKey>(entity => entity.Id, id), cancellationToken);
}

public static class CommandRepositoryExtensions
{
    public static async Task<TEntity> AddRangeAndSelectAsync<TEntity, TKey>(
        this ICommandRepository<TEntity, TKey> repository,
        IEnumerable<TEntity> entities,
        EntityMutator<TEntity>? mutate = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull
    {
        var list = entities as IList<TEntity> ?? entities.ToList();
        if (mutate is not null)
        {
            list.ForEach(entity => mutate(entity));
        }

        await repository.AddRangeAsync(list, cancellationToken).ConfigureAwait(false);
        return list[0];
    }

    public static Task UpdateWhenAsync<TEntity, TKey>(
        this ICommandRepository<TEntity, TKey> repository,
        TEntity entity,
        BusinessRule<TEntity> rule,
        EntityMutator<TEntity> mutate,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull
    {
        Guard.Ensure(entity, rule, name => new BusinessRuleViolationException($"{name} failed the update rule."));
        return repository.UpdateAsync(entity, mutate, cancellationToken);
    }

    public static Task<int> PatchAsync<TEntity, TKey>(
        this ICommandRepository<TEntity, TKey> repository,
        TKey id,
        Action<IEntityUpdater<TEntity>> update,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull =>
        repository.ExecuteUpdateAsync(
            ExpressionExtensions.Equal<TEntity, TKey>(entity => entity.Id, id),
            update,
            cancellationToken);
}

public static class UnitOfWorkExtensions
{
    public static Task<int> SaveAsync(
        this IUnitOfWork unitOfWork,
        Func<CancellationToken, Task> command,
        CancellationToken cancellationToken = default) =>
        unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await command(token).ConfigureAwait(false);
            return 0;
        }, cancellationToken);

    public static async Task<Result> TrySaveAsync(
        this IUnitOfWork unitOfWork,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Result.Failure(Error.Conflict("persistence_error", exception.Message));
        }
    }
}