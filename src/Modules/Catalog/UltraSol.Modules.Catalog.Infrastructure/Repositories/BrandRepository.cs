using UltraSol.Modules.Catalog.Domain.Catalog.Brands;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Catalog.Infrastructure.Repositories;

public sealed class BrandRepository(CatalogDbContext context) : Repository<Brand>(context), IBrandRepository;

