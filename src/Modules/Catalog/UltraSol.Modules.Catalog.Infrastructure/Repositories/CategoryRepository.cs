using UltraSol.Modules.Catalog.Domain.Catalog.Categories;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Catalog.Infrastructure.Repositories;

public sealed class CategoryRepository(CatalogDbContext context) : Repository<Category>(context), ICategoryRepository;