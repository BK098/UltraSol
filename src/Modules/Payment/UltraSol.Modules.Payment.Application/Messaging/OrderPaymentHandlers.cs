using UltraSol.Modules.Payment.Application.Contracts;
using UltraSol.Shared.Application.Messaging.Integration;
using UltraSol.Shared.IntegrationEvents.Ordering;
using PaymentAggregate = UltraSol.Modules.Payment.Domain.Payments.Payment;

namespace UltraSol.Modules.Payment.Application.Messaging;

public sealed class OrderPaymentHandlers(IPaymentStore store, TimeProvider clock) : IIntegrationHandler<OrderPlacedV1>,
    IIntegrationHandler<OrderCancelledV1>, IIntegrationHandler<OrderRejectedV1>, IIntegrationHandler<OrderCompletedV1>
{
    public async Task HandleAsync(OrderPlacedV1 message, CancellationToken ct)
    {
        var payment = await LoadAsync(message.EventId, message.OrderId, message.OrderVersion, ct);
        payment.ApplySnapshot(message.OrderVersion, message.OrderStatus, message.OrderNumber, message.GrandTotal, message.Currency,
            message.PaymentTerm, message.NetDays, message.ReservationExpiresAt, null, clock.GetUtcNow());
    }

    public async Task HandleAsync(OrderCancelledV1 message, CancellationToken ct) =>
        (await LoadAsync(message.EventId, message.OrderId, message.OrderVersion, ct)).ApplyOrderSignal(message.OrderVersion, "Cancelled", null, clock.GetUtcNow());

    public async Task HandleAsync(OrderRejectedV1 message, CancellationToken ct) =>
        (await LoadAsync(message.EventId, message.OrderId, message.OrderVersion, ct)).ApplyOrderSignal(message.OrderVersion, "Rejected", null, clock.GetUtcNow());

    public async Task HandleAsync(OrderCompletedV1 message, CancellationToken ct)
    {
        if (message.OccurredAt == default)
        {
            throw new InvalidOperationException("Completion time is required.");
        }
        (await LoadAsync(message.EventId, message.OrderId, message.OrderVersion, ct)).ApplyOrderSignal(message.OrderVersion, "Completed", message.OccurredAt, clock.GetUtcNow());
    }

    private async Task<PaymentAggregate> LoadAsync(Guid eventId, Guid orderId, long version, CancellationToken ct)
    {
        if (eventId == Guid.Empty || orderId == Guid.Empty || version <= 0)
        {
            throw new InvalidOperationException("Invalid order event identity.");
        }
        await store.LockAsync("payment:" + orderId, ct);
        var payment = await store.GetAsync(orderId, ct);
        if (payment is null)
        {
            payment = PaymentAggregate.Start(orderId, clock.GetUtcNow());
            store.Add(payment);
        }
        return payment;
    }
}
