using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Shared.Domain.Common.Repositories;
using Collection = UltraSol.Modules.Catalog.Domain.Catalog.Collections.Collection;
namespace UltraSol.Modules.Catalog.Domain.Repositories;

public interface ICollectionRepository : IRepository<Collection>
{
}