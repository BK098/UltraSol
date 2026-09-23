namespace UltraSol.Modules.Ordering.Application.Features.Payments;

public sealed record OrderPaymentSummary(Guid OrderId, string Status, decimal Amount, string Currency, string PaymentTerm,
    DateTimeOffset? DueAt, bool IsOverdue, decimal ReceivedAmount, decimal RefundedAmount, int PendingRefunds);
public sealed record OrderPaymentSession(Guid TransactionId, string PaymentUrl, DateTimeOffset ExpiresAt);

public interface IOrderPaymentClient
{
    Task<OrderPaymentSummary> GetAsync(Guid orderId, CancellationToken ct);
    Task<OrderPaymentSession> CreateSessionAsync(Guid orderId, string key, string ipAddress, CancellationToken ct);
}
