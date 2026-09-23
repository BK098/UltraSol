using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;
using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Pricing.Application.Common;

internal static class PricingInput
{
    public static bool ValidCurrency(string? value) => value is not null && value.Trim().Length == 3 &&
        value.Trim().All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

    public static bool ValidTiers(IReadOnlyList<PriceTierDto>? tiers) => tiers is { Count: > 0 } &&
        tiers.All(tier => tier is not null && tier.MinimumQuantity > 0 && tier.Amount >= 0) &&
        tiers.Select(tier => tier.MinimumQuantity).Distinct().Count() == tiers.Count;

    public static PriceTier[] Tiers(IReadOnlyList<PriceTierDto> tiers) =>
        tiers.Select(tier => PriceTier.Create(tier.MinimumQuantity, tier.Amount)).ToArray();

    public static Contract RequireContract(Contract? contract) =>
        contract ?? throw new DomainException("This price does not belong to a contract.", "ContractMismatch");
}