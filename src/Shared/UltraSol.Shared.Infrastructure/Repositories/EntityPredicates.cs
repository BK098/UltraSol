using System.Linq.Expressions;
using UltraSol.Shared.Domain.Common.Abstractions;

namespace UltraSol.Shared.Infrastructure.Repositories;

internal static class EntityPredicates
{
    public static Expression<Func<TEntity, bool>> ById<TEntity, TKey>(TKey id)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull
    {
        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        var property = Expression.Property(parameter, nameof(IEntity<>.Id));
        var equals = Expression.Equal(property, Expression.Constant(id, typeof(TKey)));
        return Expression.Lambda<Func<TEntity, bool>>(equals, parameter);
    }
}