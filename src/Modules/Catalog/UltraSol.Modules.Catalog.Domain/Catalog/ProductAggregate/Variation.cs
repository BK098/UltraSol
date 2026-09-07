using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;
namespace UltraSol.Modules.Catalog.Domain.Catalog.ProductAggregate;

/// <summary>Entity whose mutations are controlled by Product.</summary>
public sealed class Variation : BaseEntity
{
    private readonly List<VariationOption> _options = [];
    public string Name { get; private set; }
    public IReadOnlyList<VariationOption> Options => _options.AsReadOnly();
    internal Variation(string name) => Name = Guard.Required(name, "Variation name");
    internal void Rename(string name) => Name = Guard.Required(name, "Variation name");
    internal Guid AddOption(string value)
    {
        var option = new VariationOption(EnsureUnique(value));
        _options.Add(option);
        return option.Id;
    }
    internal void RenameOption(Guid id, string value)
    {
        var option = Find(id);
        option.Rename(EnsureUnique(value, id));
    }
    internal void RemoveOption(Guid id) => _options.Remove(Find(id));
    private VariationOption Find(Guid id)
    {
        Guard.Id(id);
        return _options.SingleOrDefault(x => x.Id == id)
            ?? throw new DomainException("Option does not belong to variation.");
    }
    private string EnsureUnique(string value, Guid? except = null)
    {
        var normalized = Guard.Required(value, "Option");
        if (_options.Any(x => x.Id != except && string.Equals(x.Value, normalized, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException("Option already exists.");
        return normalized;
    }
}
