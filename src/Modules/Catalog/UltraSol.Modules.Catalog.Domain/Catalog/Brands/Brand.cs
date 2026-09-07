using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Catalog.Domain.Catalog.Brands;

/// <summary>Independent aggregate root for brand metadata.</summary>
public sealed class Brand : AggregateRoot
{
    // Used only for persistence materialization; public creation still enforces business rules.
    private Brand() { Name = null!; }
    private Brand(string name, string? description)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, "Brand name");
        Description = description?.Trim();
    }

    public static Brand Create(string name, string? description = null)
    {
        return new(name, description);
    }
    public void Archive()
    {
        IsArchived = true;
    }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsArchived { get; private set; }

    public void Rename(string name)
    {
        EnsureNotArchived();
        Name = Guard.Required(name, "Brand name");
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
            throw new DomainException("Archived Brand cannot be modified.");
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