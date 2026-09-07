using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Guards;
namespace UltraSol.Modules.Catalog.Domain.Catalog.Products;

/// <summary>Entity owned by Product through Variation.</summary>
public sealed class VariationOption : BaseEntity
{
    // Used only for persistence materialization; public creation still enforces business rules.
    private VariationOption() { Value = null!; }
    public string Value { get; private set; }
    internal VariationOption(string value)
    {
        Value = Guard.Required(value, "Option");
    }
    internal void Rename(string value)
    {
        Value = Guard.Required(value, "Option");
    }
}