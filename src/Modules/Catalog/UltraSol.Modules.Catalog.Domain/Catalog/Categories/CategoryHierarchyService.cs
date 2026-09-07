using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Catalog.Domain.Catalog.Categories;

/// <summary>Validates the complete proposed parent chain before changing an aggregate.</summary>
public sealed class CategoryHierarchyService
{
    private readonly ICategoryHierarchyReader _reader;

    public CategoryHierarchyService(ICategoryHierarchyReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    public async Task<Category> CreateChildAsync(string name, Guid parentCategoryId,
        string? description = null, CancellationToken cancellationToken = default)
    {
        var category = Category.CreateRoot(name, description);
        await MoveAsync(category, parentCategoryId, cancellationToken);
        return category;
    }

    public async Task MoveAsync(Category category, Guid? parentCategoryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        cancellationToken.ThrowIfCancellationRequested();
        category.EnsureNotArchived();
        if (parentCategoryId.HasValue)
        {
            await ValidateParentChainAsync(category.Id, parentCategoryId.Value, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        category.ChangeParent(parentCategoryId);
    }

    public async Task ArchiveAsync(Category category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        cancellationToken.ThrowIfCancellationRequested();
        if (category.IsArchived)
        {
            return;
        }
        if (await _reader.HasNonArchivedChildrenAsync(category.Id, cancellationToken))
        {
            throw new DomainException("Move or archive active children before archiving their parent.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        category.Archive();
    }

    private async Task ValidateParentChainAsync(Guid categoryId, Guid parentId, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid> { categoryId };
        Guid? currentId = parentId;
        while (currentId.HasValue)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = Guard.Id(currentId.Value);
            if (!visited.Add(id))
            {
                throw new DomainException("Category hierarchy contains a cycle.");
            }
            var ancestor = await _reader.GetByIdAsync(id, cancellationToken) ?? throw new DomainException("Category ancestor does not exist.");
            if (ancestor.Id != id)
            {
                throw new DomainException("Hierarchy reader returned an incorrect category.");
            }
            ancestor.EnsureNotArchived();
            currentId = ancestor.ParentCategoryId;
        }
    }
}
