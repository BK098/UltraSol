using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Domain.Orders;
using UltraSol.Modules.Ordering.Domain.ValueObjects;

namespace UltraSol.Modules.Ordering.Application.Features.Payments;

public sealed class OrderPaymentFacade(IOrderingStore store, OrderingAccess access, IOrderPaymentClient payments, TimeProvider clock)
{
    public async Task<OrderPaymentSummary> GetAsync(Guid id, string? secret, CancellationToken ct)
    {
        await OwnedAsync(id, secret, ct);
        return await payments.GetAsync(id, ct);
    }

    public async Task<OrderPaymentSession> CreateAsync(Guid id, string? secret, string key, string ipAddress, CancellationToken ct)
    {
        var order = await OwnedAsync(id, secret, ct);
        key = OrderingRule.Text(key, 128, "Idempotency-Key");
        if (order.Status is not (OrderStatus.Placed or OrderStatus.Confirmed) || order.PaymentTerm.Type != "Prepaid"
            || order.ReservationExpiresAt <= clock.GetUtcNow())
        {
            throw new OrderingFailure(409, "PaymentNotAvailable", "This order cannot start a VNPAY session.");
        }
        return await payments.CreateSessionAsync(id, key, ipAddress, ct);
    }

    private async Task<Order> OwnedAsync(Guid id, string? secret, CancellationToken ct)
    {
        var order = await store.OrderAsync(id, ct) ?? throw new OrderingFailure(404, "NotFound", "Order was not found.");
        access.Demand(order.OwnerKey, order.GuestTokenHash, secret);
        return order;
    }
}
