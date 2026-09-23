using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.ValueObjects;

namespace UltraSol.Modules.Pricing.Domain.ValueObjects;

public sealed class EffectivePeriod : ValueObject
{
    public DateTimeOffset From { get; }
    public DateTimeOffset? To { get; }

    private EffectivePeriod(DateTimeOffset from, DateTimeOffset? to)
    {
        From = from.ToUniversalTime();
        To = to?.ToUniversalTime();
    }

    public static EffectivePeriod Create(DateTimeOffset from, DateTimeOffset? to = null)
    {
        if (to <= from)
        {
            throw new DomainException("Effective period must end after it starts.", "InvalidEffectivePeriod");
        }
        return new EffectivePeriod(from, to);
    }

    public bool Contains(DateTimeOffset at) => at >= From && (To is null || at < To);

    public bool Overlaps(EffectivePeriod other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return (To is null || other.From < To) && (other.To is null || From < other.To);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return From;
        yield return To;
    }
}