using System.Text.Json;
using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Modules.Pricing.Domain.ValueObjects;

namespace UltraSol.Modules.Pricing.Infrastructure.Persistence;

internal static class PricingJson
{
    private sealed record Tier(int MinimumQuantity, decimal Amount);
    private sealed record Period(Guid Id, DateTimeOffset From, DateTimeOffset? To, Tier[] Tiers, Guid? ContractPriceAmendmentId);
    private sealed record Proposal(Guid PricePeriodId, PriceChangeKind Kind, DateTimeOffset EffectiveFrom, Tier[] Tiers, string Reason, long ProposedRevision, Guid? ProposedBy);

    public static string SerializePeriods(IEnumerable<PricePeriod> periods) => JsonSerializer.Serialize(periods.Select(period =>
        new Period(period.Id, period.EffectivePeriod.From, period.EffectivePeriod.To, period.Tiers.Select(tier => new Tier(tier.MinimumQuantity, tier.Amount)).ToArray(), period.ContractPriceAmendmentId)));

    public static IReadOnlyList<PricePeriod> DeserializePeriods(string json) => JsonSerializer.Deserialize<Period[]>(json)!.Select(period =>
        new PricePeriod(period.Id, EffectivePeriod.Create(period.From, period.To), period.Tiers.Select(tier => PriceTier.Create(tier.MinimumQuantity, tier.Amount)), period.ContractPriceAmendmentId)).ToArray();

    public static string SerializeProposal(ContractPriceAmendment amendment) => JsonSerializer.Serialize(new Proposal(amendment.PricePeriodId, amendment.Kind,
        amendment.EffectiveFrom, amendment.Tiers.Select(tier => new Tier(tier.MinimumQuantity, tier.Amount)).ToArray(), amendment.Reason, amendment.ProposedRevision, amendment.ProposedBy));

    public static ContractPriceAmendment DeserializeAmendment(ContractPriceAmendmentRow row)
    {
        var proposal = JsonSerializer.Deserialize<Proposal>(row.ProposalJson)!;
        var amendment = new ContractPriceAmendment(row.ContractId, proposal.PricePeriodId, proposal.Kind, proposal.EffectiveFrom,
            proposal.Tiers.Select(tier => PriceTier.Create(tier.MinimumQuantity, tier.Amount)), proposal.Reason, proposal.ProposedRevision, proposal.ProposedBy, row.ProposedAt);
        amendment.RestoreDecision(row.Id, row.Status, row.DecidedBy, row.DecidedAt);
        return amendment;
    }
}