namespace UltraSol.Modules.Ordering.Domain.ValueObjects;

public sealed record BuyerSnapshot(string BuyerType, Guid? IdentityUserId, Guid? CustomerId, Guid? BusinessAccountId, string Name, string Email, string Phone)
{
    public void Validate()
    {
        OrderingRule.Require(BuyerType is "Guest" or "Registered" or "Business", "Unknown buyer type.");
        OrderingRule.Text(Name, 250, "Name");
        OrderingRule.Text(Email, 320, "Email");
        OrderingRule.Require(System.Net.Mail.MailAddress.TryCreate(Email, out var address) && address.Address == Email, "Invalid email.");
        OrderingRule.Text(Phone, 32, "Phone");
        OrderingRule.Require(IdentityUserId != Guid.Empty && CustomerId != Guid.Empty && BusinessAccountId != Guid.Empty, "References cannot be empty GUIDs.");
    }
}

public sealed record AddressSnapshot(string RecipientName, string Phone, string AddressLine1, string? AddressLine2, string? Ward,
    string? District, string? Province, string? PostalCode, string CountryCode)
{
    public void Validate()
    {
        OrderingRule.Text(RecipientName, 250, "RecipientName");
        OrderingRule.Text(Phone, 32, "Phone");
        OrderingRule.Text(AddressLine1, 500, "AddressLine1");
        OrderingRule.Require(CountryCode is { Length: 2 } && CountryCode.All(char.IsAsciiLetter), "CountryCode must contain two letters.");
        OrderingRule.Require(new[] { AddressLine2, Ward, District, Province, PostalCode }.All(value => value is null || value.Length <= 500), "Address field is too long.");
    }
}

public sealed record PaymentTerm(string Type, int? NetDays)
{
    public void Validate()
    {
        OrderingRule.Require(Type is "Prepaid" or "COD" or "Net", "Unknown payment term.");
        OrderingRule.Require(Type == "Net" ? NetDays is > 0 and <= 3650 : NetDays is null, "Invalid net payment days.");
    }
}

public sealed record PriceSnapshot(decimal ListUnitPrice, decimal FinalUnitPrice, decimal DiscountAmount, string Currency, string PriceSource,
    Guid? PriceListId, Guid? SkuPriceId, Guid? PricePeriodId, Guid? ContractPriceAmendmentId, Guid? NegotiatedPriceId, Guid? ContractId)
{
    public void Validate(string currency, int quantity)
    {
        OrderingRule.Require(Currency == currency, "All prices must have the order currency.");
        OrderingRule.Require(ListUnitPrice >= 0 && FinalUnitPrice >= 0 && DiscountAmount >= 0, "Prices cannot be negative.");
        OrderingRule.Require(checked((ListUnitPrice - FinalUnitPrice) * quantity) == DiscountAmount, "Discount does not match line prices.");
        OrderingRule.Require(PriceSource is "PriceList" or "Contract" or "Negotiated", "Unknown price source.");
    }
}

public sealed record OrderLineSnapshot(Guid ProductItemId, Guid ProductId, string ProductName, string SkuCode, string VariantDescription,
    string? ImageUrl, int Quantity, PriceSnapshot Price);

public readonly record struct Money(decimal Amount, string Currency);
public readonly record struct Quantity
{
    public int Value { get; }
    public Quantity(int value)
    {
        OrderingRule.Require(value > 0, "Quantity must be positive.");
        Value = value;
    }
}