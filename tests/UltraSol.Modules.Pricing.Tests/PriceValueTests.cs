using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Exceptions;
using Xunit;

namespace UltraSol.Modules.Pricing.Tests;

public sealed class PriceValueTests
{
    [Fact]
    public void Effective_period_uses_half_open_utc_bounds()
    {
        var start = new DateTimeOffset(2026, 9, 15, 7, 0, 0, TimeSpan.FromHours(7));
        var end = start.AddDays(1);
        var period = EffectivePeriod.Create(start, end);
        Assert.True(period.Contains(start));
        Assert.False(period.Contains(end));
        Assert.False(period.Overlaps(EffectivePeriod.Create(end)));
        Assert.True(period.Overlaps(EffectivePeriod.Create(end.AddTicks(-1))));
        Assert.Equal(TimeSpan.Zero, period.From.Offset);
        Assert.Throws<DomainException>(() => EffectivePeriod.Create(start, start));
    }

    [Fact]
    public void Currency_normalizes_ascii_code_and_prices_reject_invalid_values()
    {
        Assert.Equal(Currency.Create("VND"), Currency.Create(" vnd "));
        Assert.Throws<DomainException>(() => Currency.Create("VN"));
        Assert.Throws<DomainException>(() => Currency.Create("12$"));
        Assert.Throws<DomainException>(() => PriceTier.Create(0, 10));
        Assert.Throws<DomainException>(() => PriceTier.Create(1, -1));
        Assert.Throws<DomainException>(() => PriceList.Create(" ", Currency.Create("VND"), PriceListType.Retail));
        Assert.Throws<DomainException>(() => PriceList.Create("Retail", Currency.Create("VND"), (PriceListType)99));
    }
}