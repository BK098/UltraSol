using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Catalog.Categories;

namespace UltraSol.Modules.Catalog.Infrastructure.Persistence;

public sealed class CategoryHierarchyReader(CatalogDbContext context, ICategoryRepository repository) : ICategoryHierarchyReader
{
    public Task<Category?> GetByIdAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        repository.FindTrackedAsync(categoryId, cancellationToken);
    public Task<bool> HasNonArchivedChildrenAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        context.Categories.AnyAsync(x => x.ParentCategoryId == categoryId && !x.IsArchived, cancellationToken);
}