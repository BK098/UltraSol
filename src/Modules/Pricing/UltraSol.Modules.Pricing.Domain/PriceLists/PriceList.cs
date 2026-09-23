using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Pricing.Domain.PriceLists;

public enum PriceListType
{
    Retail,
    Wholesale,
    Contract
}

public sealed class PriceList : AggregateRoot
{
    public string Name { get; private set; }
    public Currency Currency { get; }
    public PriceListType Type { get; }

    private PriceList(string name, Currency currency, PriceListType type)
    {
        Name = name;
        Currency = currency;
        Type = type;
    }

    public static PriceList Create(string name, Currency currency, PriceListType type)
    {
        ArgumentNullException.ThrowIfNull(currency);
        if (!Enum.IsDefined(type))
        {
            throw new DomainException("Unknown price list type.", "InvalidPriceListType");
        }
        return new PriceList(Guard.Required(name, nameof(Name)), currency, type);
    }

    public void Rename(string name)
    {
        Name = Guard.Required(name, nameof(Name));
        RefreshConcurrencyStamp();
    }

    public override void MarkDeleted(string? actorId, DateTimeOffset utcNow)
    {
        throw new DomainException("Price lists cannot be deleted; historical prices must remain accessible.");
    }
}