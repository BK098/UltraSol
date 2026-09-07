using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Catalog.Infrastructure.Repositories;

public sealed class ProductRepository(CatalogDbContext context) : Repository<Product>(context), IProductRepository;

