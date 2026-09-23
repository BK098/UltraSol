using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.ValueObjects;

namespace UltraSol.Modules.Pricing.Domain.ValueObjects;

public sealed class PriceTier : ValueObject
{
    public int MinimumQuantity { get; }
    public decimal Amount { get; }

    private PriceTier(int minimumQuantity, decimal amount)
    {
        MinimumQuantity = minimumQuantity;
        Amount = amount;
    }

    public static PriceTier Create(int minimumQuantity, decimal amount)
    {
        if (minimumQuantity <= 0 || amount < 0)
        {
            throw new DomainException("Tier quantity must be positive and amount cannot be negative.", "InvalidPriceTier");
        }
        return new PriceTier(minimumQuantity, amount);
    }

    internal static PriceTier[] Validate(IEnumerable<PriceTier> tiers)
    {
        ArgumentNullException.ThrowIfNull(tiers);
        var values = tiers.ToArray();
        if (values.Length == 0 || values.Any(tier => tier is null) || values.Select(tier => tier.MinimumQuantity).Distinct().Count() != values.Length)
        {
            throw new DomainException("At least one tier with unique quantity thresholds is required.", "InvalidPriceTier");
        }
        return values.OrderBy(tier => tier.MinimumQuantity).ToArray();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MinimumQuantity;
        yield return Amount;
    }
}