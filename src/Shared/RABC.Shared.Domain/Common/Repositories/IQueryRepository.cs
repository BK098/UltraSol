using System.Linq.Expressions;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Delegates;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Domain.Common.Specifications;

namespace UltraSol.Shared.Domain.Common.Repositories
{
    public interface IQueryRepository<TEntity, TKey> where TEntity : class, IEntity<TKey> where TKey : notnull
    {
        IQueryable<TEntity> Query(bool tracking = false);
        IQueryable<TEntity> Query(Expression<Func<TEntity, bool>> predicate, bool tracking = false);
        IQueryable<TEntity> Query(ISpecification<TEntity> specification);
        IQueryable<TEntity> Query(QueryPipeline<TEntity> pipeline, bool tracking = false);
        Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
        Task<TEntity?> GetByIdAsync(TKey id, QueryPipeline<TEntity> include, CancellationToken cancellationToken = default);
        Task<TEntity> GetRequiredByIdAsync(TKey id, CancellationToken cancellationToken = default);
        Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
        Task<TEntity?> FirstOrDefaultAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);
        Task<TEntity?> SingleOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TEntity>> ListAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TProjection>> ListAsync<TProjection>(Expression<Func<TEntity, TProjection>> selector, Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);
        Task<PaginatedResult<TEntity>> GetPagedAsync(PaginationRequest page, CancellationToken cancellationToken = default);
        Task<PaginatedResult<TEntity>> GetPagedAsync(PaginationRequest page, Expression<Func<TEntity, bool>>? predicate, CancellationToken cancellationToken = default);
        Task<PaginatedResult<TEntity>> GetPagedAsync(PaginationRequest page, ISpecification<TEntity> specification, CancellationToken cancellationToken = default);
        Task<PaginatedResult<TProjection>> GetPagedAsync<TProjection>(PaginationRequest page, Expression<Func<TEntity, TProjection>> selector, Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);
        Task<bool> AnyAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);
        Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);
        Task<TResult> ExecuteAsync<TResult>(Func<IQueryable<TEntity>, CancellationToken, Task<TResult>> query, bool tracking = false, CancellationToken cancellationToken = default);
    }
    public interface IQueryRepository<TEntity> : IQueryRepository<TEntity, Guid> where TEntity : class, IEntity<Guid>;
}