using Microsoft.EntityFrameworkCore;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Domain.Common.Extensions;
using System.Linq.Expressions;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Delegates;
using UltraSol.Shared.Domain.Common.Specifications;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Infrastructure.Specifications;

namespace UltraSol.Shared.Infrastructure.Repositories;

public class QueryRepository<TEntity, TKey>(DbContext context) : IQueryRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull
{
    protected DbContext Context { get; } = context;
    protected DbSet<TEntity> Set => Context.Set<TEntity>();

    public virtual IQueryable<TEntity> Query(bool tracking = false) =>
        Set.ApplyTracking(tracking);

    public virtual IQueryable<TEntity> Query(Expression<Func<TEntity, bool>> predicate, bool tracking = false) =>
        Set.ApplyTracking(tracking).Where(predicate);

    public virtual IQueryable<TEntity> Query(ISpecification<TEntity> specification) =>
        SpecificationEvaluator.GetQuery(Set.AsQueryable(), specification);

    public virtual IQueryable<TEntity> Query(QueryPipeline<TEntity> pipeline, bool tracking = false) =>
        Set.ApplyTracking(tracking).ApplyPipeline(pipeline);

    public virtual Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default) =>
        Context.OneAsync(Set.AsNoTracking().FirstOrDefaultAsync(EntityPredicates.ById<TEntity, TKey>(id), cancellationToken), cancellationToken);

    public virtual Task<TEntity?> GetByIdAsync(
        TKey id,
        QueryPipeline<TEntity> include,
        CancellationToken cancellationToken = default) =>
        Context.OneAsync(Set.AsNoTracking().ApplyPipeline(include).FirstOrDefaultAsync(EntityPredicates.ById<TEntity, TKey>(id), cancellationToken), cancellationToken);

    public virtual async Task<TEntity> GetRequiredByIdAsync(TKey id, CancellationToken cancellationToken = default) =>
        await GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
        ?? throw EntityNotFoundException.For<TEntity>(id);

    public virtual Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        Context.OneAsync(Set.AsNoTracking().FirstOrDefaultAsync(predicate, cancellationToken), cancellationToken);

    public virtual Task<TEntity?> FirstOrDefaultAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default) =>
        Context.OneAsync(Query(specification).FirstOrDefaultAsync(cancellationToken), cancellationToken);

    public virtual Task<TEntity?> SingleOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        Context.OneAsync(Set.AsNoTracking().SingleOrDefaultAsync(predicate, cancellationToken), cancellationToken);

    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        await Context.ManyAsync(Set.AsNoTracking().ToListAsync(cancellationToken), cancellationToken).ConfigureAwait(false);

    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await Context.ManyAsync(Set.AsNoTracking().Where(predicate).ToListAsync(cancellationToken), cancellationToken).ConfigureAwait(false);

    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default) =>
        await Context.ManyAsync(Query(specification).ToListAsync(cancellationToken), cancellationToken).ConfigureAwait(false);

    public virtual async Task<IReadOnlyList<TProjection>> ListAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> selector,
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().WhereIf(predicate is not null, predicate!);
        return await query.Select(selector).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public virtual Task<PaginatedResult<TEntity>> GetPagedAsync(
        PaginationRequest page,
        CancellationToken cancellationToken = default) =>
        GetPagedAsync(page, predicate: null, cancellationToken);

    public virtual async Task<PaginatedResult<TEntity>> GetPagedAsync(
        PaginationRequest page,
        Expression<Func<TEntity, bool>>? predicate,
        CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().WhereIf(predicate is not null, predicate!);
        query = query.ApplySort(page.Sorts);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await Context.ManyAsync(query.ApplyPaging(page).ToListAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
        return PaginatedResult<TEntity>.Create(items, total, page);
    }

    public virtual async Task<PaginatedResult<TEntity>> GetPagedAsync(
        PaginationRequest page,
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        var query = SpecificationEvaluator.GetQueryWithoutPaging(Set.AsQueryable(), specification);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await Context.ManyAsync(query.ApplyPaging(page).ToListAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
        return PaginatedResult<TEntity>.Create(items, total, page);
    }

    public virtual async Task<PaginatedResult<TProjection>> GetPagedAsync<TProjection>(
        PaginationRequest page,
        Expression<Func<TEntity, TProjection>> selector,
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().WhereIf(predicate is not null, predicate!).ApplySort(page.Sorts);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.ApplyPaging(page).Select(selector).ToListAsync(cancellationToken).ConfigureAwait(false);
        return PaginatedResult<TProjection>.Create(items, total, page);
    }

    public virtual Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default) =>
        predicate is null
            ? Set.AsNoTracking().AnyAsync(cancellationToken)
            : Set.AsNoTracking().AnyAsync(predicate, cancellationToken);

    public virtual Task<bool> AnyAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default) =>
        SpecificationEvaluator.GetQueryWithoutPaging(Set.AsQueryable(), specification)
            .AnyAsync(cancellationToken);

    public virtual Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default) =>
        predicate is null
            ? Set.AsNoTracking().CountAsync(cancellationToken)
            : Set.AsNoTracking().CountAsync(predicate, cancellationToken);

    public virtual Task<int> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default) =>
        SpecificationEvaluator.GetQueryWithoutPaging(Set.AsQueryable(), specification)
            .CountAsync(cancellationToken);

    public virtual async Task<TResult> ExecuteAsync<TResult>(
        Func<IQueryable<TEntity>, CancellationToken, Task<TResult>> query,
        bool tracking = false,
        CancellationToken cancellationToken = default)
    {
        var result = await query(Set.ApplyTracking(tracking), cancellationToken).ConfigureAwait(false);
        if (Context is IRepositoryMaterializer loader)
        {
            if (result is TEntity entity) await loader.MaterializeAsync(entity, cancellationToken).ConfigureAwait(false);
            else if (result is IEnumerable<TEntity> entities)
                foreach (var item in entities) await loader.MaterializeAsync(item, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }
}

public class QueryRepository<TEntity>(DbContext context)
    : QueryRepository<TEntity, Guid>(context), IQueryRepository<TEntity>
    where TEntity : class, IEntity<Guid>;
