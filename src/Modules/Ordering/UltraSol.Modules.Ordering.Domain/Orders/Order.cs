using UltraSol.Modules.Ordering.Domain.ValueObjects;
using UltraSol.Modules.Ordering.Domain.Events;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Events;

namespace UltraSol.Modules.Ordering.Domain.Orders;

public enum OrderType { Retail, Wholesale, Contract }
public enum OrderStatus { Placed, Confirmed, Completed, Cancelled, Rejected }
public sealed record ReservedStockLine(Guid ProductItemId, int Quantity);

public sealed class Order : AggregateRoot
{
    private readonly List<OrderLine> _lines = [];
    private readonly List<OrderHistory> _history = [];
    private readonly List<OrderNote> _notes = [];
    private Order() { }
    public string OrderNumber { get; private set; } = "";
    public Guid? CartId { get; private set; }
    public string OwnerKey { get; private set; } = "";
    public Guid? IdentityUserId { get; private set; }
    public string? GuestTokenHash { get; private set; }
    public OrderType OrderType { get; private set; }
    public string OrderSource { get; private set; } = "";
    public BuyerSnapshot Buyer { get; private set; } = null!;
    public AddressSnapshot ShippingAddress { get; private set; } = null!;
    public AddressSnapshot? BillingAddress { get; private set; }
    public PaymentTerm PaymentTerm { get; private set; } = null!;
    public Guid PricingQuoteId { get; private set; }
    public Guid InventoryReservationId { get; private set; }
    public Guid? ContractRef { get; private set; }
    public DateTimeOffset ReservationExpiresAt { get; private set; }
    public string Currency { get; private set; } = "";
    public IReadOnlyList<ReservedStockLine> ReservedStock { get; private set; } = [];
    public decimal Subtotal { get; private set; }
    public decimal DiscountTotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal ShippingAmount { get; private set; }
    public decimal GrandTotal { get; private set; }
    public OrderStatus Status { get; private set; }
    public long OrderVersion { get; private set; }
    public DateTimeOffset PlacedAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();
    public IReadOnlyCollection<OrderHistory> History => _history.AsReadOnly();
    public IReadOnlyCollection<OrderNote> Notes => _notes.AsReadOnly();

    public static Order Place(Guid id, string number, Guid? cartId, string ownerKey, Guid? userId, string? guestHash, OrderType type,
        string source, BuyerSnapshot buyer, AddressSnapshot shipping, AddressSnapshot? billing, PaymentTerm term, Guid quoteId,
        Guid reservationId, DateTimeOffset expiresAt, string currency, IReadOnlyList<OrderLineSnapshot> lines, DateTimeOffset now, Guid? contractRef = null,
        IReadOnlyList<ReservedStockLine>? reservedStock = null)
    {
        OrderingRule.Require(id != Guid.Empty && quoteId != Guid.Empty && reservationId != Guid.Empty, "Order references are required.");
        OrderingRule.Require(Enum.IsDefined(type) && lines.Count is > 0 and <= 100, "Invalid order type or lines.");
        OrderingRule.Require(type != OrderType.Contract || contractRef is { } contract && contract != Guid.Empty, "Contract order requires a contract.");
        OrderingRule.Require(type == OrderType.Contract || contractRef is null, "Unexpected contract reference.");
        OrderingRule.Require(lines.Select(line => line.ProductItemId).Distinct().Count() == lines.Count, "Duplicate order lines.");
        reservedStock ??= lines.Select(line => new ReservedStockLine(line.ProductItemId, line.Quantity)).ToArray();
        OrderingRule.Require(reservedStock.Count > 0 && reservedStock.All(line => line.ProductItemId != Guid.Empty && line.Quantity > 0)
            && reservedStock.Select(line => line.ProductItemId).Distinct().Count() == reservedStock.Count, "Invalid reserved stock snapshot.");
        buyer.Validate();
        shipping.Validate();
        billing?.Validate();
        term.Validate();
        var order = new Order { Id = id, OrderNumber = OrderingRule.Text(number, 64, "OrderNumber"), CartId = cartId,
            OwnerKey = OrderingRule.Text(ownerKey, 100, "Owner"), IdentityUserId = userId, GuestTokenHash = guestHash, OrderType = type,
            OrderSource = source, Buyer = buyer, ShippingAddress = shipping, BillingAddress = billing, PaymentTerm = term,
            PricingQuoteId = quoteId, InventoryReservationId = reservationId, ReservationExpiresAt = expiresAt,
            Currency = OrderingRule.Currency(currency), ContractRef = contractRef, PlacedAt = now, ReservedStock = reservedStock.ToArray() };
        foreach (var line in lines)
        {
            OrderingRule.Require(line.ProductId != Guid.Empty && line.ProductItemId != Guid.Empty && line.Quantity > 0, "Invalid order line.");
            OrderingRule.Text(line.ProductName, 500, "ProductName");
            OrderingRule.Text(line.SkuCode, 250, "SkuCode");
            line.Price.Validate(order.Currency, line.Quantity);
            order._lines.Add(new OrderLine(line));
            order.Subtotal = checked(order.Subtotal + line.Price.ListUnitPrice * line.Quantity);
            order.DiscountTotal = checked(order.DiscountTotal + line.Price.DiscountAmount);
        }
        order.GrandTotal = checked(order.Subtotal - order.DiscountTotal);
        order.MarkCreated(userId?.ToString(), now);
        order.Transition(OrderStatus.Placed, null, now);
        return order;
    }

    public void Confirm(DateTimeOffset now)
    {
        OrderingRule.Require(Status == OrderStatus.Placed, "Only placed orders can be confirmed.");
        ConfirmedAt = now;
        Transition(OrderStatus.Confirmed, null, now);
    }

    public void Cancel(string reason, DateTimeOffset now)
    {
        OrderingRule.Require(Status is OrderStatus.Placed or OrderStatus.Confirmed, "Order cannot be cancelled.");
        reason = OrderingRule.Text(reason, 2000, "CancellationReason");
        CancelledAt = now;
        Transition(OrderStatus.Cancelled, reason, now);
    }

    public void Reject(string reason, DateTimeOffset now)
    {
        OrderingRule.Require(Status == OrderStatus.Placed, "Only placed orders can be rejected.");
        Transition(OrderStatus.Rejected, OrderingRule.Text(reason, 2000, "RejectionReason"), now);
    }

    public void Complete(DateTimeOffset now)
    {
        OrderingRule.Require(Status == OrderStatus.Confirmed, "Only confirmed orders can be completed.");
        CompletedAt = now;
        Transition(OrderStatus.Completed, null, now);
    }

    public void AddNote(string text, Guid? actor, DateTimeOffset now)
    {
        _notes.Add(new OrderNote(OrderingRule.Text(text, 4000, "Note"), actor, now));
        MarkUpdated(actor?.ToString(), now);
    }

    private void Transition(OrderStatus status, string? reason, DateTimeOffset now)
    {
        Status = status;
        OrderVersion++;
        _history.Add(new OrderHistory(status, OrderVersion, reason, now));
        MarkUpdated(IdentityUserId?.ToString(), now);
        DomainEvent changed = status switch
        {
            OrderStatus.Placed => new OrderPlaced(Id, OrderVersion),
            OrderStatus.Confirmed => new OrderConfirmed(Id, OrderVersion),
            OrderStatus.Rejected => new OrderRejected(Id, OrderVersion, reason!),
            OrderStatus.Cancelled => new OrderCancelled(Id, OrderVersion, reason!),
            OrderStatus.Completed => new OrderCompleted(Id, OrderVersion),
            _ => throw new InvalidOperationException("Unknown order status.")
        };
        AddDomainEvent(changed);
    }
}