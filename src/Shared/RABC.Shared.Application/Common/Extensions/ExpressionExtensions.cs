using Domain.Common.Delegates;
using System.Linq.Expressions;

namespace Domain.Common.Extensions
{
    public sealed class ParameterReplaceVisitor : ExpressionVisitor
    {
        private readonly Dictionary<ParameterExpression, Expression> _map;

        public ParameterReplaceVisitor(Dictionary<ParameterExpression, Expression> map)
        {
            _map = map;
        }

        public ParameterReplaceVisitor(ParameterExpression from, Expression to)
            : this(new Dictionary<ParameterExpression, Expression> { [from] = to })
        {
        }

        protected override Expression VisitParameter(ParameterExpression node) =>
            _map.TryGetValue(node, out var replacement) ? replacement : base.VisitParameter(node);
    }
    /// <summary>
    /// Provides extension methods for working with LINQ expressions.
    /// </summary>
    public static class ExpressionExtensions
    {
        /// <summary>
        /// Combines two expressions using a logical AND operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>

        public static Expression<Func<T, bool>> And<T>(
            this Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right) =>
            left.Compose(right, Expression.AndAlso);
        /// <summary>
        /// Combines two expressions using a logical OR operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>

        public static Expression<Func<T, bool>> Or<T>(
            this Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right) =>
            left.Compose(right, Expression.OrElse);
        /// <summary>
        /// Negates the given expression, effectively inverting its logic.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>

        public static Expression<Func<T, bool>> Not<T>(this Expression<Func<T, bool>> expression)
        {
            ArgumentNullException.ThrowIfNull(expression);
            return Expression.Lambda<Func<T, bool>>(Expression.Not(expression.Body), expression.Parameters);
        }
        /// <summary>
        /// Conditionally combines two expressions using a logical AND operation based on the specified condition.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="left"></param>
        /// <param name="condition"></param>
        /// <param name="right"></param>
        /// <returns></returns>

        public static Expression<Func<T, bool>> AndIf<T>(
            this Expression<Func<T, bool>> left,
            bool condition,
            Expression<Func<T, bool>> right) =>
            condition ? left.And(right) : left;
        /// <summary>
        /// Conditionally combines two expressions using a logical OR operation based on the specified condition.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="left"></param>
        /// <param name="condition"></param>
        /// <param name="right"></param>
        /// <returns></returns>

        public static Expression<Func<T, bool>> OrIf<T>(
            this Expression<Func<T, bool>> left,
            bool condition,
            Expression<Func<T, bool>> right) =>
            condition ? left.Or(right) : left;
        /// <summary>
        /// Conditionally combines an expression with a filter delegate using a logical AND operation based on the specified condition.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="left"></param>
        /// <param name="condition"></param>
        /// <param name="right"></param>
        /// <returns></returns>

        public static Expression<Func<T, bool>> AndIf<T>(
            this Expression<Func<T, bool>> left,
            bool condition,
            QueryFilter<T> right)
            where T : class =>
            condition ? left.And(right()) : left;

        /// <summary>
        /// Combines two expressions into a single expression using the specified merge function.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="merge"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static Expression<T> Compose<T>(
            this Expression<T> left,
            Expression<T> right,
            Func<Expression, Expression, BinaryExpression> merge)
        {
            ArgumentNullException.ThrowIfNull(left);
            ArgumentNullException.ThrowIfNull(right);
            ArgumentNullException.ThrowIfNull(merge);

            var map = left.Parameters
                .Select((parameter, index) => new { parameter, replacement = right.Parameters[index] })
                .ToDictionary(x => x.replacement, x => (Expression)x.parameter);

            var rightBody = new ParameterReplaceVisitor(map).Visit(right.Body)
                            ?? throw new InvalidOperationException("Failed to rebind expression parameters.");

            return Expression.Lambda<T>(merge(left.Body, rightBody), left.Parameters);
        }

        /// <summary>
        /// Combines a collection of predicate expressions into a single expression using the specified merge function.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="predicates"></param>
        /// <param name="merge"></param>
        /// <returns></returns>
        public static Expression<Func<T, bool>> Combine<T>(this IEnumerable<Expression<Func<T, bool>>> predicates,
        Func<Expression, Expression, BinaryExpression>? merge = null)
        {
            merge ??= Expression.AndAlso;
            using var enumerator = predicates.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return static _ => true;
            }
            var aggregate = enumerator.Current;
            while (enumerator.MoveNext())
            {
                aggregate = aggregate.Compose(enumerator.Current, merge);
            }
            return aggregate;
        }

        /// <summary>
        /// Gets the member name from a given expression, typically used for property access.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        public static string GetMemberName<T, TProperty>(this Expression<Func<T, TProperty>> expression)
        {
            ArgumentNullException.ThrowIfNull(expression);
            return GetMemberPath(expression.Body);
        }

        /// <summary>
        /// Gets the member path from a given expression, typically used for nested property access.
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static string GetMemberPath(this Expression expression)
        {
            return expression switch
            {
                MemberExpression member => GetMemberPath(member),
                UnaryExpression { Operand: var operand } => GetMemberPath(operand),
                _ => throw new ArgumentException("Expression must be a member access.", nameof(expression))
            };
        }

        /// <summary>
        /// Gets the member path from a given MemberExpression, typically used for nested property access.
        /// </summary>
        /// <param name="member"></param>
        /// <returns></returns>
        private static string GetMemberPath(MemberExpression member)
        {
            var parts = new Stack<string>();
            Expression? current = member;
            while (current is MemberExpression memberExpression)
            {
                parts.Push(memberExpression.Member.Name);
                current = memberExpression.Expression;
            }
            return string.Join('.', parts);
        }

        /// <summary>
        /// Creates a property expression from a given property path string, allowing dynamic access to nested properties.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="propertyPath"></param>
        /// <returns></returns>
        public static Expression<Func<T, TProperty>> PropertyExpression<T, TProperty>(string propertyPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyPath);
            var parameter = Expression.Parameter(typeof(T), "x");
            Expression body = parameter;
            foreach (var segment in propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                body = Expression.PropertyOrField(body, segment);
            }
            if (body.Type != typeof(TProperty))
            {
                body = Expression.Convert(body, typeof(TProperty));
            }
            return Expression.Lambda<Func<T, TProperty>>(body, parameter);
        }

        /// <summary>
        /// Creates a property lambda expression from a given property path string, allowing dynamic access to nested properties.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="propertyPath"></param>
        /// <returns></returns>
        public static LambdaExpression PropertyLambda<T>(string propertyPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyPath);
            var parameter = Expression.Parameter(typeof(T), "x");
            Expression body = parameter;
            foreach (var segment in propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                body = Expression.PropertyOrField(body, segment);
            }
            if (body.Type.IsValueType)
            {
                body = Expression.Convert(body, typeof(object));
            }
            return Expression.Lambda(body, parameter);
        }

        /// <summary>
        /// Creates an equality expression for a given property and value, allowing dynamic filtering based on property values.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="property"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Expression<Func<T, bool>> Equal<T, TProperty>(
            Expression<Func<T, TProperty>> property,
            TProperty value)
        {
            var constant = Expression.Constant(value, typeof(TProperty));
            return Expression.Lambda<Func<T, bool>>(Expression.Equal(property.Body, constant), property.Parameters);
        }

        /// <summary>
        /// Creates a case-insensitive "contains" expression for a given string property and value, allowing dynamic filtering based on substring matches.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="property"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Expression<Func<T, bool>> ContainsIgnoreCase<T>(
            Expression<Func<T, string?>> property,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return static _ => true;
            }

            var parameter = property.Parameters[0];
            var member = property.Body;
            var toLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
            var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
            var notNull = Expression.NotEqual(member, Expression.Constant(null, typeof(string)));
            var body = Expression.AndAlso(
                notNull,
                Expression.Call(Expression.Call(member, toLower), contains, Expression.Constant(value.ToLowerInvariant())));

            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }

        //public static TDelegate CompileCached<TDelegate>(this Expression<TDelegate> expression)
        //    where TDelegate : Delegate =>
        //    ExpressionCache<TDelegate>.GetOrAdd(expression);
    }

}
