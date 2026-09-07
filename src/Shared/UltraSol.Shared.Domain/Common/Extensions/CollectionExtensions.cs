using System.Linq.Expressions;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Delegates;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Domain.Common.Results;

namespace UltraSol.Shared.Domain.Common.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<T> WhereIf<T>(
        this IEnumerable<T> source,
        bool condition,
        Func<T, bool> predicate) =>
        condition ? source.Where(predicate) : source;

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
        where T : class
    {
        foreach (var item in source)
        {
            if (item is not null)
            {
                yield return item;
            }
        }
    }

    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source)
        {
            action(item);
        }
    }

    public static void ForEach<T>(this IEnumerable<T> source, Action<T, int> action)
    {
        var index = 0;
        foreach (var item in source)
        {
            action(item, index++);
        }
    }

    public static async Task ForEachAsync<T>(
        this IEnumerable<T> source,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action(item, cancellationToken).ConfigureAwait(false);
        }
    }

    public static IEnumerable<TResult> MapIf<T, TResult>(
        this IEnumerable<T> source,
        Func<T, bool> predicate,
        Func<T, TResult> whenTrue,
        Func<T, TResult> whenFalse) =>
        source.Select(item => predicate(item) ? whenTrue(item) : whenFalse(item));

    public static TOut Pipe<TIn, TOut>(this TIn value, Func<TIn, TOut> mapper) => mapper(value);

    public static T Tap<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }

    public static IReadOnlyList<T> AsReadOnlyList<T>(this IEnumerable<T> source) =>
        source as IReadOnlyList<T> ?? source.ToList();

    public static PaginatedResult<T> ToPagedResult<T>(
        this IEnumerable<T> source,
        PaginationRequest page)
    {
        var materialized = source as IList<T> ?? source.ToList();
        var items = materialized.Skip(page.Skip).Take(page.Take).ToList();
        return PaginatedResult<T>.Create(items, materialized.Count, page);
    }

    public static IEnumerable<T> Apply<T>(this IEnumerable<T> source, Func<IEnumerable<T>, IEnumerable<T>> pipeline) =>
        pipeline(source);

    public static bool None<T>(this IEnumerable<T> source) => !source.Any();

    public static bool None<T>(this IEnumerable<T> source, Func<T, bool> predicate) => !source.Any(predicate);
}

public static class AuditableExtensions
{
    public static T StampCreated<T>(this T entity, string? actorId, DateTimeOffset utcNow)
        where T : ICreated
    {
        entity.MarkCreated(actorId, utcNow);
        return entity;
    }

    public static T StampModified<T>(this T entity, string? actorId, DateTimeOffset utcNow)
        where T : IUpdated
    {
        entity.MarkUpdated(actorId, utcNow);
        return entity;
    }

    public static T StampDeleted<T>(this T entity, string? actorId, DateTimeOffset utcNow)
        where T : ISoftDeleted
    {
        entity.MarkDeleted(actorId, utcNow);
        return entity;
    }

    public static T StampRestored<T>(this T entity)
        where T : ISoftDeleted
    {
        entity.Restore();
        return entity;
    }

    public static void ApplyAudit(
        this object entity,
        AuditStampContext context,
        AuditStampAction? customStamper = null)
    {
        if (customStamper is not null)
        {
            customStamper(entity, context);
            return;
        }

        switch (context.Operation)
        {
            case AuditOperation.Insert when entity is ICreated created:
                created.MarkCreated(context.ActorId, context.UtcNow);
                break;
            case AuditOperation.Update when entity is IUpdated modified:
                modified.MarkUpdated(context.ActorId, context.UtcNow);
                break;
            case AuditOperation.SoftDelete when entity is ISoftDeleted deleted:
                deleted.MarkDeleted(context.ActorId, context.UtcNow);
                break;
            case AuditOperation.Restore when entity is ISoftDeleted restored:
                restored.Restore();
                break;
            case AuditOperation.Delete:
                break;
        }
    }

    public static bool IsActive(this ISoftDeleted entity) => !entity.IsDeleted;

    public static Expression<Func<T, bool>> NotDeleted<T>()
        where T : class, ISoftDeleted =>
        entity => !entity.IsDeleted;
}

public static class ResultExtensions
{
    public static Result<T> ToResult<T>(this T? value, Error error)
        where T : class =>
        value is null ? Result.Failure<T>(error) : Result.Success(value);

    public static Result<T> Ensure<T>(this Result<T> result, BusinessRule<T> rule, Error error) =>
        result.IsFailure
            ? result
            : rule(result.Value)
                ? result
                : Result.Failure<T>(error);

    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> resultTask,
        BusinessRule<T> rule,
        Error error)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.Ensure(rule, error);
    }

    public static Result<T> Tap<T>(this Result<T> result, Action<T> action)
    {
        if (result.IsSuccess)
        {
            action(result.Value);
        }

        return result;
    }
}