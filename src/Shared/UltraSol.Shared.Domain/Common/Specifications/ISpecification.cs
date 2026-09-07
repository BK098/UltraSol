using System.Linq.Expressions;
using UltraSol.Shared.Domain.Common.Delegates;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Shared.Domain.Common.Specifications
{
    public sealed record OrderExpression<T>(LambdaExpression KeySelector, SortDirection Direction);

    public interface ISpecification<T> where T : class
    {
        Expression<Func<T, bool>> Criteria { get; }
        IReadOnlyList<Expression<Func<T, object>>> Includes { get; }
        IReadOnlyList<string> IncludeStrings { get; }
        IReadOnlyList<OrderExpression<T>> OrderExpressions { get; }
        int? Skip { get; }
        int? Take { get; }
        bool AsNoTracking { get; }
        bool IgnoreQueryFilters { get; }
        bool AsSplitQuery { get; }
    }

    public interface ISpecificationBuilder<T> where T : class
    {
        ISpecificationBuilder<T> Where(Expression<Func<T, bool>> predicate);
        ISpecificationBuilder<T> WhereIf(bool condition, Expression<Func<T, bool>> predicate);
        ISpecificationBuilder<T> WhereIf(bool condition, QueryFilter<T> filter);
        ISpecificationBuilder<T> Include(Expression<Func<T, object>> include);
        ISpecificationBuilder<T> Include(string includeString);
        ISpecificationBuilder<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector);
        ISpecificationBuilder<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector);
        ISpecificationBuilder<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector);
        ISpecificationBuilder<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector);
        ISpecificationBuilder<T> Page(int pageNumber, int pageSize);
        ISpecificationBuilder<T> SkipTake(int skip, int take);
        ISpecificationBuilder<T> AsNoTracking(bool enabled = true);
        ISpecificationBuilder<T> IgnoreQueryFilters(bool enabled = true);
        ISpecificationBuilder<T> AsSplitQuery(bool enabled = true);
        ISpecificationBuilder<T> Apply(QueryPipeline<T> pipeline);
    }
}