using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Domain.Carts;
using UltraSol.Modules.Ordering.Domain.Orders;
using UltraSol.Shared.Application.Messaging.Integration;
using UltraSol.Shared.IntegrationEvents.Inventory;

namespace UltraSol.Modules.Ordering.Application.Messaging;

public sealed class ReservationExpiredHandler(IOrderingStore store, TimeProvider clock, IOrderingOutbox outbox) : IIntegrationHandler<InventoryReservationExpiredV1>
{
    public async Task HandleAsync(InventoryReservationExpiredV1 message, CancellationToken ct)
    {
        if (message.EventId == Guid.Empty || message.OrderId == Guid.Empty || message.ReservationId == Guid.Empty || message.ExpiredAt == default)
        {
            throw new InvalidOperationException("Invalid reservation expiry.");
        }
        await store.LockAsync("order:" + message.OrderId, ct);
        var attempt = await store.AttemptByOrderAsync(message.OrderId, ct);
        if (attempt is not null)
        {
            await store.LockAsync("attempt:" + attempt.Id, ct);
            if (attempt.ReservationId is { } reservationId && reservationId != message.ReservationId)
            {
                throw new InvalidOperationException("Reservation expiry reference mismatch.");
            }
            attempt.ExpiryObserved = true;
            if (attempt.Stage is not (CheckoutStage.Completed or CheckoutStage.Failed))
            {
                attempt.Stage = CheckoutStage.Failed;
                attempt.ErrorCode = "ReservationExpired";
                attempt.ErrorMessage = "Inventory reservation expired.";
                attempt.ErrorStatus = 409;
                attempt.LeaseToken = null;
                attempt.LeaseUntil = null;
                if (attempt.CartId is { } cartId)
                {
                    var cart = await store.CartAsync(cartId, ct);
                    if (cart?.Status == CartStatus.CheckingOut)
                    {
                        cart.EndCheckout(false, clock.GetUtcNow());
                    }
                }
            }
        }
        var order = await store.OrderAsync(message.OrderId, ct);
        if (order is null)
        {
            return;
        }
        if (order.InventoryReservationId != message.ReservationId)
        {
            throw new InvalidOperationException("Reservation expiry reference mismatch.");
        }
        var cancel = await store.FindOperationAsync(order.Id, "Cancel", ct);
        if (order.Status is OrderStatus.Placed or OrderStatus.Confirmed)
        {
            if (order.Status == OrderStatus.Placed && cancel is null)
            {
                order.Reject("InventoryReservationExpired", message.ExpiredAt);
            }
            else
            {
                order.Cancel(cancel?.Reason ?? "InventoryReservationExpired", message.ExpiredAt);
            }
            outbox.Changed(order);
        }
        if (cancel?.State == "Pending" && order.Status == OrderStatus.Cancelled)
        {
            cancel.State = "Completed";
            cancel.UpdatedAt = clock.GetUtcNow();
            cancel.LeaseToken = null;
            cancel.LeaseUntil = null;
        }
    }
}
