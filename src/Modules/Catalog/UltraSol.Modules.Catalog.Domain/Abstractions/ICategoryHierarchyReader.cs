using UltraSol.Modules.Catalog.Domain.Catalog.Categories;

namespace UltraSol.Modules.Catalog.Domain.Abstractions;

/// <summary>Reads current hierarchy state; persistence and concurrency protection are supplied externally.</summary>
public interface ICategoryHierarchyReader
{
    Task<Category?> GetByIdAsync(Guid categoryId, CancellationToken cancellationToken = default);
    Task<bool> HasNonArchivedChildrenAsync(Guid categoryId, CancellationToken cancellationToken = default);
}
