using Domain.Common.Delegates;
using Domain.Common.Paging;
using Domain.Common.Specifications;
using System.Linq.Expressions;

namespace Domain.Common.Extensions
{
    public static class QueryableExtensions
    {
        /// <summary>
        /// Applies a filter to the query only if the specified condition is true, allowing for conditional query building.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="condition"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IQueryable<T> WhereIf<T>(this IQueryable<T> query, bool condition, Expression<Func<T, bool>> predicate)
        {
            return condition ? query.Where(predicate) : query;
        }

        /// <summary>
        /// Applies a filter to the query only if the specified condition is true, allowing for conditional query building using a QueryFilter delegate.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="condition"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static IQueryable<T> WhereIf<T>(this IQueryable<T> query, bool condition, QueryFilter<T> filter) where T : class
        {
            return condition ? query.Where(filter()) : query;
        }

        /// <summary>
        /// Applies a filter to the query only if the specified value is not null, allowing for conditional query building based on the presence of a value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="query"></param>
        /// <param name="value"></param>
        /// <param name="predicateFactory"></param>
        /// <returns></returns>
        public static IQueryable<T> WhereIfNotNull<T, TValue>(this IQueryable<T> query, TValue? value, Func<TValue, Expression<Func<T, bool>>> predicateFactory) where TValue : class
        {
            return value is null ? query : query.Where(predicateFactory(value));
        }

        /// <summary>
        /// Applies a filter to the query only if the specified nullable value is not null, allowing for conditional query building based on the presence of a value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="query"></param>
        /// <param name="value"></param>
        /// <param name="predicateFactory"></param>
        /// <returns></returns>
        public static IQueryable<T> WhereIfNotNull<T, TValue>(this IQueryable<T> query, TValue? value, Func<TValue, Expression<Func<T, bool>>> predicateFactory) where TValue : struct
        {
            return value is null ? query : query.Where(predicateFactory(value.Value));
        }

        /// <summary>
        /// Applies a query pipeline to the query, allowing for dynamic composition of query operations based on a provided pipeline delegate.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="pipeline"></param>
        /// <returns></returns>
        public static IQueryable<T> ApplyPipeline<T>(this IQueryable<T> query, QueryPipeline<T>? pipeline) where T : class
        {
            return pipeline is null ? query : pipeline(query);
        }

        /// <summary>
        /// Applies a series of query operators to the query, allowing for dynamic composition of query operations based on a provided array of operator delegates.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="operators"></param>
        /// <returns></returns>
        public static IQueryable<T> Apply<T>(this IQueryable<T> query, params Func<IQueryable<T>, IQueryable<T>>[] operators)
        {
            return operators.Aggregate(query, (current, op) => op(current));
        }

        /// <summary>
        /// Applies an ordering to the query based on a specified property and direction, allowing for dynamic sorting of query results.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="query"></param>
        /// <param name="condition"></param>
        /// <param name="keySelector"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public static IQueryable<T> OrderByIf<T, TKey>(this IQueryable<T> query, bool condition, Expression<Func<T, TKey>> keySelector, SortDirection direction = SortDirection.Ascending)
        {
            if (!condition)
            {
                return query;
            }
            return direction == SortDirection.Descending
                ? query.OrderByDescending(keySelector)
                : query.OrderBy(keySelector);
        }

        /// <summary>
        /// Applies paging to the query based on specified skip and take values, allowing for dynamic pagination of query results.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="skip"></param>
        /// <param name="take"></param>
        /// <returns></returns>
        public static IQueryable<T> ApplyPaging<T>(this IQueryable<T> query, int skip, int take)
        {
            if (skip > 0)
            {
                query = query.Skip(skip);
            }
            if (take > 0)
            {
                query = query.Take(take);
            }
            return query;
        }

        /// <summary>
        /// Applies paging to the query based on a specified PaginationRequest, allowing for dynamic pagination of query results.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public static IQueryable<T> ApplyPaging<T>(this IQueryable<T> query, PaginationRequest page)
        {
            return query.Skip(page.Skip).Take(page.Take);
        }

        /// <summary>
        /// Applies sorting to the query based on a collection of SortDescriptor objects, allowing for dynamic sorting of query results based on multiple fields and directions.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="sorts"></param>
        /// <returns></returns>
        public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, IEnumerable<SortDescriptor> sorts)
        {
            IOrderedQueryable<T>? ordered = null;
            var isFirst = true;
            foreach (var sort in sorts)
            {
                var lambda = ExpressionExtensions.PropertyLambda<T>(sort.Field);
                ordered = isFirst
                    ? query.OrderByLambda(lambda, sort.Direction)
                    : ordered!.ThenByLambda(lambda, sort.Direction);
                isFirst = false;
            }
            return ordered ?? query;
        }

        /// <summary>
        /// Applies sorting to the query based on a collection of OrderExpression objects, allowing for dynamic sorting of query results based on multiple fields and directions.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="orders"></param>
        /// <returns></returns>
        public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, IEnumerable<OrderExpression<T>> orders)
        {
            IOrderedQueryable<T>? ordered = null;
            var isFirst = true;
            foreach (var order in orders)
            {
                ordered = isFirst
                    ? query.OrderByLambda(order.KeySelector, order.Direction)
                    : ordered!.ThenByLambda(order.KeySelector, order.Direction);
                isFirst = false;
            }
            return ordered ?? query;
        }

        /// <summary>
        /// Applies a series of query operators to the query, allowing for dynamic composition of query operations based on a provided array of operator delegates.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="propertyPath"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public static IOrderedQueryable<T> OrderByProperty<T>(this IQueryable<T> query, string propertyPath, SortDirection direction = SortDirection.Ascending)
        {
            return query.OrderByLambda(ExpressionExtensions.PropertyLambda<T>(propertyPath), direction);
        }

        /// <summary>
        /// Applies an ordering to the query based on a specified LambdaExpression and direction, allowing for dynamic sorting of query results.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="keySelector"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public static IOrderedQueryable<T> OrderByLambda<T>(this IQueryable<T> query, LambdaExpression keySelector, SortDirection direction)
        {
            var methodName = direction == SortDirection.Descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy);
            return ApplyOrder(query, keySelector, methodName);
        }

        /// <summary>
        /// Applies a subsequent ordering to an already ordered query based on a specified LambdaExpression and direction, allowing for dynamic sorting of query results.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="keySelector"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public static IOrderedQueryable<T> ThenByLambda<T>(this IOrderedQueryable<T> query, LambdaExpression keySelector, SortDirection direction)
        {
            var methodName = direction == SortDirection.Descending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy);
            return ApplyOrder(query, keySelector, methodName);
        }

        /// <summary>
        /// Applies an ordering to the query based on a specified LambdaExpression and direction, allowing for dynamic sorting of query results.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="keySelector"></param>
        /// <param name="methodName"></param>
        /// <returns></returns>
        private static IOrderedQueryable<T> ApplyOrder<T>(IQueryable<T> query, LambdaExpression keySelector, string methodName)
        {
            var result = typeof(Queryable)
                .GetMethods()
                .Single(method =>
                   method.Name == methodName
                && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), keySelector.ReturnType)
                .Invoke(null, [query, keySelector]);
            return (IOrderedQueryable<T>)result!;
        }

        /// <summary>
        /// Applies a specification to the query, allowing for dynamic filtering, sorting, and pagination of query results based on a provided specification object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="specification"></param>
        /// <returns></returns>
        public static IQueryable<T> ApplySpecification<T>(this IQueryable<T> query, ISpecification<T> specification) where T : class
        {
            ArgumentNullException.ThrowIfNull(specification);
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
    }
}