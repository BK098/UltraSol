using UltraSol.Shared.Domain.Common.Abstractions;

namespace UltraSol.Shared.Domain.Common.Repositories;

public interface IRepository<TEntity, TKey> : IQueryRepository<TEntity, TKey>, ICommandRepository<TEntity, TKey>
where TEntity : class, IEntity<TKey>
where TKey : notnull;
public interface IRepository<TEntity> : IRepository<TEntity, Guid>, IQueryRepository<TEntity>, ICommandRepository<TEntity>
   where TEntity : class, IEntity<Guid>;
