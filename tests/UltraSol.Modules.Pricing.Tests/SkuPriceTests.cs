using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Exceptions;
using Xunit;

namespace UltraSol.Modules.Pricing.Tests;

public sealed class SkuPriceTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public void Immediate_change_preserves_next_schedule_and_historical_amounts()
    {
        var price = Create();
        price.SetInitialPrice(Start, [PriceTier.Create(1, 100)], Actor, Start);
        price.SchedulePriceChange(Start.AddMonths(1), [PriceTier.Create(1, 110)], Actor, Start);
        price.ChangePriceImmediately([PriceTier.Create(1, 120)], Actor, Start.AddDays(14));
        Assert.Equal(100, price.Resolve(1, Start.AddDays(14).AddTicks(-1))!.Amount);
        Assert.Equal(120, price.Resolve(1, Start.AddDays(14))!.Amount);
        Assert.Equal(110, price.Resolve(1, Start.AddMonths(1))!.Amount);
        Assert.Equal(3, price.Periods.Count);
        Assert.Equal(price.Periods[1].EffectivePeriod.From, price.Periods[0].EffectivePeriod.To);
    }

    [Fact]
    public void Schedule_edit_reschedule_and_cancel_leave_auditable_before_and_after_values()
    {
        var price = Create();
        price.SetInitialPrice(Start, [PriceTier.Create(1, 100)], Actor, Start);
        var first = price.SchedulePriceChange(Start.AddMonths(1), [PriceTier.Create(1, 110)], Actor, Start);
        price.SchedulePriceChange(Start.AddMonths(2), [PriceTier.Create(1, 130)], Actor, Start);
        price.ChangeScheduledPrice(first, [PriceTier.Create(1, 115)], Actor, Start.AddDays(1));
        price.ReschedulePriceChange(first, Start.AddDays(20), Actor, Start.AddDays(2));
        Assert.Equal(115, price.Resolve(1, Start.AddDays(20))!.Amount);
        price.CancelScheduledPrice(first, Actor, Start.AddDays(3));
        Assert.Equal(100, price.Resolve(1, Start.AddMonths(1))!.Amount);
        Assert.Equal(130, price.Resolve(1, Start.AddMonths(2))!.Amount);
        Assert.Contains(price.Changes, change => change.Before.Any(period => period.Tiers[0].Amount == 110));
        Assert.Contains(price.Changes, change => change.Before.Any(period => period.Tiers[0].Amount == 115));
        Assert.All(price.Changes, change => Assert.Equal(Actor, change.ActorId));
    }

    [Fact]
    public void Duplicate_start_invalid_tiers_and_started_period_edits_leave_state_unchanged()
    {
        var price = Create();
        var initial = price.SetInitialPrice(Start, [PriceTier.Create(1, 100)], Actor, Start);
        var scheduled = price.SchedulePriceChange(Start.AddMonths(1), [PriceTier.Create(1, 110)], Actor, Start);
        var stamp = price.ConcurrencyStamp;
        Assert.Throws<DomainException>(() => price.SchedulePriceChange(Start.AddMonths(1), [PriceTier.Create(1, 120)], Actor, Start));
        Assert.Throws<DomainException>(() => price.ChangeScheduledPrice(scheduled, [PriceTier.Create(1, 1), PriceTier.Create(1, 2)], Actor, Start));
        Assert.Throws<DomainException>(() => price.CancelScheduledPrice(initial, Actor, Start));
        Assert.Throws<DomainException>(() => price.ReschedulePriceChange(scheduled, Start, Actor, Start));
        Assert.Equal(stamp, price.ConcurrencyStamp);
        Assert.Equal(2, price.Changes.Count);
        Assert.Equal(100, price.Resolve(1, Start)!.Amount);
    }

    [Fact]
    public void Tier_threshold_selects_one_unit_price_for_the_entire_quantity()
    {
        var price = Create();
        var tiers = new[] { PriceTier.Create(50, 85_000), PriceTier.Create(10, 90_000) };
        price.SetInitialPrice(Start, tiers, Actor, Start);
        tiers[0] = PriceTier.Create(1, 1);
        Assert.Null(price.Resolve(9, Start));
        Assert.Equal(90_000, price.Resolve(49, Start)!.Amount);
        Assert.Equal(85_000, price.Resolve(60, Start)!.Amount);
        Assert.Null(price.Resolve(60, Start.AddTicks(-1)));
        Assert.Throws<DomainException>(() => price.Resolve(0, Start));
    }

    [Fact]
    public void Each_sku_has_its_own_mutation_stamp_and_timeline()
    {
        var list = PriceList.Create("Retail VN", Currency.Create("VND"), PriceListType.Retail);
        var first = SkuPrice.Create(list, Guid.NewGuid());
        var second = SkuPrice.Create(list, Guid.NewGuid());
        var firstStamp = first.ConcurrencyStamp;
        var secondStamp = second.ConcurrencyStamp;
        first.SetInitialPrice(Start, [PriceTier.Create(1, 100)], Actor, Start);
        Assert.NotEqual(firstStamp, first.ConcurrencyStamp);
        Assert.Equal(secondStamp, second.ConcurrencyStamp);
        Assert.Empty(second.Periods);
    }

    private static SkuPrice Create() => SkuPrice.Create(PriceList.Create("Retail VN", Currency.Create("VND"), PriceListType.Retail), Guid.NewGuid());
}