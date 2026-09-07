using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Repositories;
using Collection = UltraSol.Modules.Catalog.Domain.Catalog.Collections.Collection;
namespace UltraSol.Modules.Catalog.Infrastructure.Repositories;

public sealed class CollectionRepository(CatalogDbContext context) : Repository<Collection>(context), ICollectionRepository;

