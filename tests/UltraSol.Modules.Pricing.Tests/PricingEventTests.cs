using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.Contracts.Events;
using UltraSol.Modules.Pricing.Domain.Negotiations;
using UltraSol.Modules.Pricing.Domain.Negotiations.Events;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Modules.Pricing.Domain.Prices.Events;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Exceptions;
using Xunit;

namespace UltraSol.Modules.Pricing.Tests;

public sealed class PricingEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Timeline_event_preserves_future_effective_time_and_failed_changes_emit_nothing()
    {
        var price = SkuPrice.Create(PriceList.Create("Retail", Currency.Create("VND"), PriceListType.Retail), Guid.NewGuid());
        price.SetInitialPrice(Now, [PriceTier.Create(1, 100)], null, Now);
        price.ClearDomainEvents();
        var scheduled = price.SchedulePriceChange(Now.AddDays(10), [PriceTier.Create(1, 110)], null, Now);
        var changed = Assert.IsType<SkuPriceTimelineChanged>(Assert.Single(price.DomainEvents));
        Assert.Equal(price.Id, changed.SkuPriceId);
        Assert.Equal(price.SkuId, changed.SkuId);
        Assert.Equal(price.Revision, changed.Revision);
        Assert.Equal(Now, changed.OccurredAt);
        Assert.Null(changed.Change.ActorId);
        Assert.Equal(Now.AddDays(10), changed.Change.After.Single(period => period.Id == scheduled).EffectivePeriod.From);
        Assert.Equal(100, price.Resolve(1, Now)!.Amount);
        Assert.Throws<DomainException>(() => price.SchedulePriceChange(Now.AddDays(10), [PriceTier.Create(1, 120)], null, Now));
        Assert.Single(price.DomainEvents);
    }

    [Fact]
    public void Contract_amendment_emits_timeline_change_only_after_approval()
    {
        var list = PriceList.Create("Contract", Currency.Create("VND"), PriceListType.Contract);
        var contract = Contract.Create(Guid.NewGuid(), list, EffectivePeriod.Create(Now, Now.AddMonths(1)), "Terms");
        var price = SkuPrice.Create(list, Guid.NewGuid(), contract);
        price.SetInitialPrice(Now, [PriceTier.Create(1, 100)], null, Now, contract);
        price.ClearDomainEvents();
        contract.Activate(null, Now);
        Assert.Equal(ContractStatus.Active, Assert.IsType<ContractStatusChanged>(Assert.Single(contract.DomainEvents)).Status);
        var amendment = price.ProposeContractPriceChange(contract, Now.AddDays(10), [PriceTier.Create(1, 120)], "Agreed", null, Now);
        Assert.Empty(price.DomainEvents);
        price.ApproveContractPriceAmendment(contract, amendment, null, Now);
        var changed = Assert.IsType<SkuPriceTimelineChanged>(Assert.Single(price.DomainEvents));
        Assert.Equal(contract.Id, changed.ContractId);
        Assert.Equal(amendment, changed.Change.ContractPriceAmendmentId);
        contract.ClearDomainEvents();
        contract.Terminate(null, Now.AddDays(1));
        Assert.Equal(ContractStatus.Terminated, Assert.IsType<ContractStatusChanged>(Assert.Single(contract.DomainEvents)).Status);
    }

    [Fact]
    public void Negotiation_events_follow_successful_transitions_with_no_fabricated_actor()
    {
        var price = NegotiatedPrice.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 80, Currency.Create("VND"),
            PriceListType.Wholesale, null, "Agreed", EffectivePeriod.Create(Now, Now.AddDays(10)), null, Now);
        Assert.Null(price.ProposedBy);
        price.Submit(null, Now);
        price.Approve(null, Now);
        price.Revoke(null, Now.AddDays(1));
        var events = price.DomainEvents.Cast<NegotiatedPriceStatusChanged>().ToArray();
        Assert.Equal([NegotiatedPriceStatus.PendingApproval, NegotiatedPriceStatus.Approved, NegotiatedPriceStatus.Revoked], events.Select(value => value.Status));
        Assert.All(events, value => Assert.Equal(price.TransactionId, value.TransactionId));
        Assert.Equal(3, events.Select(value => value.EventId).Distinct().Count());
        Assert.Throws<DomainException>(() => price.Approve(null, Now.AddDays(1)));
        Assert.Equal(3, price.DomainEvents.Count);
    }
}