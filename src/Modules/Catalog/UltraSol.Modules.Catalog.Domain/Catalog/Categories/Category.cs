using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Catalog.Domain.Catalog.Categories;

/// <summary>One node is one aggregate. Hierarchy mutations go through CategoryHierarchyService.</summary>
public sealed class Category : AggregateRoot
{
    // Used only for persistence materialization; public creation still enforces business rules.
    private Category() { Name = null!; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsArchived { get; private set; }
    public Guid? ParentCategoryId { get; private set; }

    private Category(string name, string? description)
    {
        Name = Guard.Required(name, "Category name");
        Description = description?.Trim();
    }

    public static Category CreateRoot(string name, string? description = null)
    {
        return new(name, description);
    }

    internal void ChangeParent(Guid? parentId)
    {
        EnsureNotArchived();
        if (parentId.HasValue)
        {
            Guard.Id(parentId.Value);
            if (parentId == Id)
            {
                throw new DomainException("Category cannot be its own parent.");
            }
        }
        ParentCategoryId = parentId;
    }

    public void Archive()
    {
        IsArchived = true;
    }

    public void Rename(string name)
    {
        EnsureNotArchived();
        Name = Guard.AgainstNullOrWhiteSpace(name, "Category name");
    }

    public void ChangeDescription(string? description)
    {
        EnsureNotArchived();
        Description = description?.Trim();
    }

    internal void EnsureNotArchived()
    {
        if (IsArchived)
        {
            throw new DomainException("Archived Category cannot be modified.");
        }
    }

    public override void MarkDeleted(string? actorId, DateTimeOffset utcNow)
    {
        throw new DomainException("Use Archive instead of soft-delete.");
    }
    public override void Restore()
    {
        throw new DomainException("Archived catalog aggregates cannot be restored.");
    }
}
