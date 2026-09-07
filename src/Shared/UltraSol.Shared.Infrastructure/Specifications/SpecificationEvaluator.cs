using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using UltraSol.Shared.Domain.Common.Extensions;
using UltraSol.Shared.Domain.Common.Specifications;

namespace UltraSol.Shared.Infrastructure.Specifications;

public static class SpecificationEvaluator
{
    public static IQueryable<TEntity> GetQuery<TEntity>(
        IQueryable<TEntity> inputQuery,
        ISpecification<TEntity> specification)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(specification);

        var query = inputQuery;

        if (specification.IgnoreQueryFilters)
        {
            query = query.IgnoreQueryFilters();
        }

        if (specification.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (specification.AsSplitQuery)
        {
            query = query.AsSplitQuery();
        }

        query = specification.Includes.Aggregate(query, (current, include) => current.Include(include));
        query = specification.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));
        query = query.Where(specification.Criteria);
        query = query.ApplySort(specification.OrderExpressions);

        if (specification.Skip is > 0)
        {
            query = query.Skip(specification.Skip.Value);
        }

        if (specification.Take is > 0)
        {
            query = query.Take(specification.Take.Value);
        }

        return query;
    }

    public static IQueryable<TEntity> GetQueryWithoutPaging<TEntity>(
        IQueryable<TEntity> inputQuery,
        ISpecification<TEntity> specification)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(specification);

        var query = inputQuery;

        if (specification.IgnoreQueryFilters)
        {
            query = query.IgnoreQueryFilters();
        }

        if (specification.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (specification.AsSplitQuery)
        {
            query = query.AsSplitQuery();
        }

        query = specification.Includes.Aggregate(query, (current, include) => current.Include(include));
        query = specification.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));
        query = query.Where(specification.Criteria);
        return query.ApplySort(specification.OrderExpressions);
    }
}

public static class EfQueryableExtensions
{
    public static IQueryable<T> ApplyTracking<T>(this IQueryable<T> query, bool tracking)
        where T : class =>
        tracking ? query.AsTracking() : query.AsNoTracking();

    public static IQueryable<T> ApplyIncludes<T>(
        this IQueryable<T> query,
        params Expression<Func<T, object>>[] includes)
        where T : class =>
        includes.Aggregate(query, (current, include) => current.Include(include));

    public static IQueryable<T> TagWithIf<T>(this IQueryable<T> query, bool condition, string tag) =>
        condition ? query.TagWith(tag) : query;
}
