using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;
using UltraSol.Shared.Domain.Common.ValueObjects;

namespace UltraSol.Modules.Pricing.Domain.ValueObjects;

public sealed class Currency : ValueObject
{
    public string Code { get; }

    private Currency(string code) => Code = code;

    public static Currency Create(string code)
    {
        var normalized = Guard.Required(code, nameof(Currency)).ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(character => character is < 'A' or > 'Z'))
        {
            throw new DomainException("Currency must be a three-letter code.", "InvalidCurrency");
        }
        return new Currency(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Code;
    }
}