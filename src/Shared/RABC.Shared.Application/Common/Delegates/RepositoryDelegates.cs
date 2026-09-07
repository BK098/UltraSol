using System.Linq.Expressions;

namespace Domain.Common.Delegates;

/// <summary>
/// Composes an additional LINQ pipeline over an entity query (includes, filters, projections prep).
/// </summary>
public delegate IQueryable<TEntity> QueryPipeline<TEntity>(IQueryable<TEntity> query)
    where TEntity : class;

/// <summary>
/// Builds a filter expression, typically from request/search criteria.
/// </summary>
public delegate Expression<Func<TEntity, bool>> QueryFilter<TEntity>()
    where TEntity : class;

/// <summary>
/// Mutates an entity before it is persisted (command path).
/// </summary>
public delegate void EntityMutator<in TEntity>(TEntity entity)
    where TEntity : class;

/// <summary>
/// Async intercept hook around a command operation.
/// </summary>
public delegate ValueTask EntityInterceptor<in TEntity>(TEntity entity, CancellationToken cancellationToken)
    where TEntity : class;

/// <summary>
/// Applies audit stamps for a single tracked instance.
/// </summary>
public delegate void AuditStampAction(object entity, AuditStampContext context);

/// <summary>
/// Selects a sort key for dynamic ordering.
/// </summary>
public delegate Expression<Func<TEntity, object?>> SortKeySelector<TEntity>(TEntity _)
    where TEntity : class;

/// <summary>
/// Maps an entity to a projection without allocating an intermediate collection.
/// </summary>
public delegate TProjection EntityProjector<in TEntity, out TProjection>(TEntity entity);

/// <summary>
/// Factory used when a missing entity should be created instead of throwing.
/// </summary>
public delegate TEntity EntityFactory<out TEntity>();

/// <summary>
/// Predicate used by guard and business-rule helpers.
/// </summary>
public delegate bool BusinessRule<in T>(T candidate);

/// <summary>
/// Produces a domain exception when a rule fails.
/// </summary>
public delegate Exception ExceptionFactory(string argumentName);

public readonly record struct AuditStampContext(
    string? ActorId,
    DateTimeOffset UtcNow,
    AuditOperation Operation);

public enum AuditOperation
{
    Insert = 1,
    Update = 2,
    Delete = 3,
    SoftDelete = 4,
    Restore = 5
}
