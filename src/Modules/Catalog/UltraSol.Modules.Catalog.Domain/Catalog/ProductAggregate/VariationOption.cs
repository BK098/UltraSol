using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Guards;
namespace UltraSol.Modules.Catalog.Domain.Catalog.ProductAggregate;

/// <summary>Entity owned by Product through Variation.</summary>
public sealed class VariationOption : BaseEntity
{
    public string Value { get; private set; }
    internal VariationOption(string value) => Value = Guard.Required(value, "Option");
    internal void Rename(string value) => Value = Guard.Required(value, "Option");
}