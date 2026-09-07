namespace UltraSol.Modules.Catalog.Domain.Repositories;

public interface ICollectionProductQuery
{
    Task<IReadOnlyList<Guid>> GetProductIdsAsync(Guid collectionId, Guid? afterId = null,
        int pageSize = 50, CancellationToken cancellationToken = default);
}

