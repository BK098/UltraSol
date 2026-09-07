using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;
using UltraSol.Shared.Domain.Common.ValueObjects;
namespace UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;

/// <summary>SKU value; global uniqueness is a persistence responsibility.</summary>
public sealed class SKU : ValueObject
{
    public string Value { get; }
    private SKU(string value) => Value = value;
    public static SKU Create(string value)
    {
        var normalized = Guard.Required(value, "SKU").ToUpperInvariant();
        if (normalized.Length > 64) throw new DomainException("SKU cannot exceed 64 characters.");
        return new SKU(normalized);
    }
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}