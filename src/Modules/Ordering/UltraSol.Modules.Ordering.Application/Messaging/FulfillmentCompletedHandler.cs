using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Shared.Application.Messaging.Integration;
using UltraSol.Shared.IntegrationEvents.Fulfillment;

namespace UltraSol.Modules.Ordering.Application.Messaging;

public sealed class FulfillmentCompletedHandler(IOrderingStore store, TimeProvider clock) : IIntegrationHandler<FulfillmentCompletedV1>
{
    public async Task HandleAsync(FulfillmentCompletedV1 message, CancellationToken ct)
    {
        if (message.EventId == Guid.Empty || message.OrderId == Guid.Empty || message.ReservationId == Guid.Empty || message.FulfillmentId == Guid.Empty || message.CompletedAt == default)
        {
            throw new InvalidOperationException("Invalid fulfillment completion.");
        }
        await store.LockAsync("order:" + message.OrderId, ct);
        var order = await store.OrderAsync(message.OrderId, ct) ?? throw new InvalidOperationException("Fulfillment order has not arrived.");
        if (order.InventoryReservationId != message.ReservationId)
        {
            throw new InvalidOperationException("Fulfillment reservation mismatch.");
        }
        var previous = await store.FindOperationAsync(message.OrderId, "Complete", ct);
        if (previous is not null)
        {
            if (previous.FulfillmentId != message.FulfillmentId || previous.ReservationId != message.ReservationId || previous.CompletedAt != message.CompletedAt)
            {
                throw new InvalidOperationException("Conflicting fulfillment completion.");
            }
            return;
        }
        store.Add(new OrderOperation { Kind = "Complete", OrderId = message.OrderId, ReservationId = message.ReservationId,
            FulfillmentId = message.FulfillmentId, CompletedAt = message.CompletedAt, CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow() });
    }
}