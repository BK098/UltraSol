using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Domain.Repositories;
using UltraSol.Modules.Ordering.Domain.ValueObjects;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Ordering.Application.Features.Orders;

public sealed class OrderReadService(IOrderingStore store, IOrderingUnitOfWork unit, OrderingAccess access, TimeProvider clock)
{
    public sealed record HistoryItem(string Status, long Version, string? Reason, DateTimeOffset OccurredAt);
    public sealed record NoteItem(Guid Id, string Text, Guid? ActorId, DateTimeOffset CreatedAt);
    public sealed record OrderDetails(Guid Id, string OrderNumber, string OrderType, string OrderSource, string Status, string ConcurrencyStamp,
        BuyerSnapshot Buyer, AddressSnapshot ShippingAddress, AddressSnapshot? BillingAddress, PaymentTerm PaymentTerm, string Currency,
        decimal Subtotal, decimal DiscountTotal, decimal TaxTotal, decimal ShippingAmount, decimal GrandTotal, Guid PricingQuoteId,
        Guid InventoryReservationId, DateTimeOffset ReservationExpiresAt, Guid? ContractRef, DateTimeOffset PlacedAt,
        OrderLineSnapshot[] Lines, HistoryItem[] History, NoteItem[]? Notes);

    public async Task<ApiResult<OrderDetails>> GetAsync(Guid id, string? secret, bool admin, CancellationToken ct)
    {
        var order = await store.OrderAsync(id, ct) ?? throw new OrderingFailure(404, "NotFound", "Order was not found.");
        if (!admin)
        {
            access.Demand(order.OwnerKey, order.GuestTokenHash, secret);
        }
        return ApiResultBuilder.Success(new OrderDetails(order.Id, order.OrderNumber, order.OrderType.ToString(), order.OrderSource,
            order.Status.ToString(), order.ConcurrencyStamp, order.Buyer, order.ShippingAddress, order.BillingAddress, order.PaymentTerm,
            order.Currency, order.Subtotal, order.DiscountTotal, order.TaxTotal, order.ShippingAmount, order.GrandTotal,
            order.PricingQuoteId, order.InventoryReservationId, order.ReservationExpiresAt, order.ContractRef, order.PlacedAt,
            order.Lines.Select(line => line.Snapshot).ToArray(), order.History.OrderBy(item => item.Version)
                .Select(item => new HistoryItem(item.Status.ToString(), item.Version, item.Reason, item.OccurredAt)).ToArray(),
            admin ? order.Notes.OrderBy(item => item.CreatedAt).Select(item => new NoteItem(item.Id, item.Text, item.ActorId, item.CreatedAt)).ToArray() : null));
    }

    public Task<ApiResult<Guid>> AddNoteAsync(Guid id, string stamp, string text, CancellationToken ct) => unit.ExecuteInTransactionAsync(async token =>
    {
        var order = await store.OrderAsync(id, token) ?? throw new OrderingFailure(404, "NotFound", "Order was not found.");
        if (order.ConcurrencyStamp != stamp)
        {
            throw new OrderingFailure(409, "OrderConcurrencyConflict", "Order changed. Reload and retry.");
        }
        order.AddNote(text, access.UserId, clock.GetUtcNow());
        return ApiResultBuilder.Success(order.Notes.Last().Id, statusCode: 201);
    }, ct);
}