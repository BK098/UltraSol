using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Catalog.Infrastructure.Repositories
{
    public class CatalogUnitOfWork : UnitOfWork, ICatalogUnitOfWork
    {
        public CatalogUnitOfWork(CatalogDbContext context, IDomainEventDispatcher domainEventDispatcher) : base(context, domainEventDispatcher)
        {
        }
    }
}