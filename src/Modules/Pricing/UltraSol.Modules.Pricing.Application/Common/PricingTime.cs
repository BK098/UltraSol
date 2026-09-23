namespace UltraSol.Modules.Pricing.Application.Common;

public static class PricingTime
{
    public static DateTimeOffset Normalize(DateTimeOffset value) =>
        new(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);
}