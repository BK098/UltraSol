using PaymentAggregate = UltraSol.Modules.Payment.Domain.Payments.Payment;
using UltraSol.Modules.Payment.Domain.Payments;

namespace UltraSol.Modules.Payment.Application.Contracts;

public interface IPaymentStore
{
    void Reset();
    Task LockAsync(string key, CancellationToken cancellationToken);
    Task<PaymentAggregate?> GetAsync(Guid orderId, CancellationToken cancellationToken);
    Task<PaymentAggregate?> FindByProviderReferenceAsync(string reference, CancellationToken cancellationToken);
    Task<Guid[]> PendingGatewayOrderIdsAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task<Guid[]> PendingRefundOrderIdsAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task<PaymentAggregate[]> ListAsync(int skip, int take, bool overdue, DateTimeOffset now, CancellationToken cancellationToken);
    Task<PaymentRefund[]> ListRefundsAsync(int skip, int take, string? state, CancellationToken cancellationToken);
    void Add(PaymentAggregate payment);
}
