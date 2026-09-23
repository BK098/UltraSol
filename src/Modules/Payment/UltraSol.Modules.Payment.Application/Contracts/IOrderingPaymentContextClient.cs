namespace UltraSol.Modules.Payment.Application.Contracts;

public sealed record OrderingPaymentContext(Guid OrderId, long OrderVersion, string OrderStatus, string OrderNumber, decimal Amount,
    string Currency, string PaymentTerm, int? NetDays, DateTimeOffset ReservationExpiresAt, DateTimeOffset? CompletedAt);

public interface IOrderingPaymentContextClient
{
    Task<OrderingPaymentContext> GetAsync(Guid orderId, CancellationToken ct);
}
