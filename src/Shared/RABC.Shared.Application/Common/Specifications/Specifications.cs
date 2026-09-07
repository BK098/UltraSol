using Domain.Common.Delegates;
using Domain.Common.Extensions;
using Domain.Common.Paging;
using System.Linq.Expressions;

namespace Domain.Common.Specifications
{
    public abstract class Specification<T> : ISpecification<T> where T : class
    {
        public Expression<Func<T, bool>> Criteria { get; protected set; } = static _ => true;
        public List<Expression<Func<T, object>>> IncludeExpressions { get; } = [];
        public List<string> IncludeStringList { get; } = [];
        public List<OrderExpression<T>> OrderExpressionList { get; } = [];

        public IReadOnlyList<Expression<Func<T, object>>> Includes => IncludeExpressions;
        public IReadOnlyList<string> IncludeStrings => IncludeStringList;
        public IReadOnlyList<OrderExpression<T>> OrderExpressions => OrderExpressionList;

        public int? Skip { get; protected set; }
        public int? Take { get; protected set; }
        public bool AsNoTracking { get; protected set; } = true;
        public bool IgnoreQueryFilters { get; protected set; }
        public bool AsSplitQuery { get; protected set; }

        protected void Where(Expression<Func<T, bool>> predicate) =>
            Criteria = Criteria.And(predicate);

        protected void Include(Expression<Func<T, object>> include) =>
            IncludeExpressions.Add(include);

        protected void Include(string includeString) =>
            IncludeStringList.Add(includeString);

        protected void OrderBy<TKey>(Expression<Func<T, TKey>> keySelector) =>
            OrderExpressionList.Add(new OrderExpression<T>(keySelector, SortDirection.Ascending));

        protected void OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector) =>
            OrderExpressionList.Add(new OrderExpression<T>(keySelector, SortDirection.Descending));

        protected void ApplyPaging(int pageNumber, int pageSize)
        {
            var page = PaginationRequest.Create(pageNumber, pageSize);
            Skip = page.Skip;
            Take = page.Take;
        }
    }

    public sealed class BuiltSpecification<T> : Specification<T>, ISpecificationBuilder<T>
        where T : class
    {
        ISpecificationBuilder<T> ISpecificationBuilder<T>.Where(Expression<Func<T, bool>> predicate)
        {
            Where(predicate);
            return this;
        }

        ISpecificationBuilder<T> ISpecificationBuilder<T>.WhereIf(bool condition, Expression<Func<T, bool>> predicate)
        {
            if (condition)
            {
                Where(predicate);
            }

            return this;
        }

        ISpecificationBuilder<T> ISpecificationBuilder<T>.WhereIf(bool condition, QueryFilter<T> filter)
        {
            if (condition)
            {
                Where(filter());
            }

            return this;
        }

        ISpecificationBuilder<T> ISpecificationBuilder<T>.Include(Expression<Func<T, object>> include)
        {
            Include(include);
            return this;
        }

        ISpecificationBuilder<T> ISpecificationBuilder<T>.Include(string includeString)
        {
            Include(includeString);
            return this;
        }

        ISpecificationBuilder<T> ISpecificationBuilder<T>.OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            OrderBy(keySelector);
            return this;
        }

        ISpecificationBuilder<T> ISpecificationBuilder<T>.OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            OrderByDescending(keySelector);
            return this;
        }

        ISpecificationBuilder<T> ISpecificationBuilder<T>.ThenBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            OrderBy(keySelector);
            return this;
        }

        ISpecificationBuilder<T> ISpecificationBuilder<T>.ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            OrderByDescending(keySelector);
            return this;
        }

        ISpecificationBuilder<T> ISpecificationBuilder<T>.Page(int pageNumber, int pageSize)
        {
            ApplyPaging(pageNumber, pageSize);
            return this;
        }

        ISpecificationBuilder<T> ISpecificationBuilder<T>.SkipTake(int skip, int take)
        {
            Skip = skip;
            Take = take;
            return this;
        }

        ISpecificationBuilder<T> ISpecificationBuilder<T>.AsNoTracking(bool enabled)
        {
            AsNoTracking = enabled;
            return this;
        }

        ISpecificationBuilder<T> ISpecificationBuilder<T>.IgnoreQueryFilters(bool enabled)
        {
            IgnoreQueryFilters = enabled;
            return this;
        }

        ISpecificationBuilder<T> ISpecificationBuilder<T>.AsSplitQuery(bool enabled)
        {
            AsSplitQuery = enabled;
            return this;
        }

        ISpecificationBuilder<T> ISpecificationBuilder<T>.Apply(QueryPipeline<T> pipeline) => this;
    }

    public static class Spec
    {
        public static ISpecification<T> For<T>(Action<ISpecificationBuilder<T>> configure)
            where T : class
        {
            ArgumentNullException.ThrowIfNull(configure);
            var specification = new BuiltSpecification<T>();
            configure(specification);
            return specification;
        }

        public static ISpecification<T> Where<T>(Expression<Func<T, bool>> predicate)
            where T : class =>
            For<T>(s => s.Where(predicate));

        public static ISpecification<T> All<T>()
            where T : class =>
            new BuiltSpecification<T>();
    }

    public static class SpecificationExtensions
    {
        public static ISpecification<T> And<T>(this ISpecification<T> left, ISpecification<T> right)
            where T : class =>
            Combine(left, right, (l, r) => l.And(r));

        public static ISpecification<T> Or<T>(this ISpecification<T> left, ISpecification<T> right)
            where T : class =>
            Combine(left, right, (l, r) => l.Or(r));

        public static ISpecification<T> Not<T>(this ISpecification<T> specification)
            where T : class =>
            Spec.For<T>(builder =>
            {
                builder.Where(specification.Criteria.Not());
                CopyShape(specification, builder);
            });

        public static ISpecification<T> With<T>(
            this ISpecification<T> specification,
            Action<ISpecificationBuilder<T>> configure)
            where T : class =>
            Spec.For<T>(builder =>
            {
                builder.Where(specification.Criteria);
                CopyShape(specification, builder);
                configure(builder);
            });

        private static ISpecification<T> Combine<T>(
            ISpecification<T> left,
            ISpecification<T> right,
            Func<Expression<Func<T, bool>>, Expression<Func<T, bool>>, Expression<Func<T, bool>>> merge)
            where T : class =>
            Spec.For<T>(builder =>
            {
                builder.Where(merge(left.Criteria, right.Criteria));
                CopyShape(left, builder);
                CopyShape(right, builder);
            });

        private static void CopyShape<T>(ISpecification<T> source, ISpecificationBuilder<T> builder)
            where T : class
        {
            foreach (var include in source.Includes)
            {
                builder.Include(include);
            }

            foreach (var include in source.IncludeStrings)
            {
                builder.Include(include);
            }

            if (builder is BuiltSpecification<T> built)
            {
                built.OrderExpressionList.AddRange(source.OrderExpressions);
                return;
            }

            foreach (var order in source.OrderExpressions)
            {
                if (order.Direction == SortDirection.Ascending)
                {
                    builder.OrderBy(Adapt<T>(order.KeySelector));
                }
                else
                {
                    builder.OrderByDescending(Adapt<T>(order.KeySelector));
                }
            }
        }

        private static Expression<Func<T, object>> Adapt<T>(LambdaExpression keySelector)
            where T : class
        {
            var parameter = keySelector.Parameters[0];
            var body = keySelector.Body.Type.IsValueType
                ? Expression.Convert(keySelector.Body, typeof(object))
                : keySelector.Body;
            return Expression.Lambda<Func<T, object>>(body, parameter);
        }
    }
}