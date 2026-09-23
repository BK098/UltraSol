using UltraSol.Modules.Ordering.Domain.Orders;
using UltraSol.Modules.Ordering.Domain.ValueObjects;

namespace UltraSol.Modules.Ordering.Application.Checkout;

public sealed record BuyerContact(string Name, string Email, string Phone);
public sealed record CheckoutCartRequest(string ConcurrencyStamp, BuyerContact Buyer, AddressSnapshot ShippingAddress, AddressSnapshot? BillingAddress, PaymentTerm PaymentTerm);
public sealed record RequestedLine(Guid ProductItemId, int Quantity, Guid? NegotiatedPriceId = null);
public sealed record AssistedOrderRequest(string Currency, OrderType OrderType, BuyerSnapshot Buyer, AddressSnapshot ShippingAddress,
    AddressSnapshot? BillingAddress, PaymentTerm PaymentTerm, RequestedLine[] Lines, Guid? ContractId = null, Guid? NegotiationTransactionRef = null);
public sealed record CheckoutInput(Guid? CartId, string Currency, OrderType OrderType, BuyerSnapshot Buyer, AddressSnapshot ShippingAddress,
    AddressSnapshot? BillingAddress, PaymentTerm PaymentTerm, RequestedLine[] Lines, Guid? ContractId, Guid? NegotiationTransactionRef, string? CartVersion);
public sealed record StockLine(Guid ProductItemId, int Quantity);
public sealed record CatalogItem(Guid ProductItemId, Guid ProductId, string SkuCode, string ProductName, string VariantDescription,
    string? ImageUrl, bool IsSellable, string? ReasonCode, bool IsBundle, StockLine[] Components);
public sealed record CatalogItems(CatalogItem[] Items);
public sealed record QuoteRequest(string OrderType, string Currency, Guid? CustomerId, Guid? ContractId, Guid? NegotiationTransactionRef, RequestedLine[] Lines);
public sealed record QuoteLine(Guid ProductItemId, int Quantity, decimal ListUnitPrice, decimal FinalUnitPrice, decimal DiscountAmount,
    decimal LineTotal, string PriceSource, Guid? PriceListId, Guid? SkuPriceId, Guid? PricePeriodId,
    Guid? ContractPriceAmendmentId, Guid? NegotiatedPriceId, Guid? ContractId)
{
    public PriceSnapshot Snapshot(string currency) => new(ListUnitPrice, FinalUnitPrice, DiscountAmount, currency, PriceSource,
        PriceListId, SkuPriceId, PricePeriodId, ContractPriceAmendmentId, NegotiatedPriceId, ContractId);
}
public sealed record PriceQuote(Guid QuoteId, DateTimeOffset QuotedAt, string Currency, QuoteLine[] Lines, decimal Subtotal,
    decimal DiscountTotal, decimal TaxTotal, decimal ShippingAmount, decimal GrandTotal);
public sealed record ReservationLine(Guid WarehouseId, Guid ProductItemId, int Quantity);
public sealed record Reservation(Guid Id, Guid OrderId, string Status, DateTimeOffset ExpiresAt, ReservationLine[] Lines);
public sealed record ReserveRequest(Guid OrderId, DateTimeOffset ExpiresAt, StockLine[] Lines);
public sealed record CheckoutResult(Guid OperationId, string Stage, Guid? OrderId, string? OrderNumber, decimal? GrandTotal,
    string? Currency, string? OrderStatus, string? ConcurrencyStamp, DateTimeOffset? ReservationExpiresAt, string? ErrorCode = null);