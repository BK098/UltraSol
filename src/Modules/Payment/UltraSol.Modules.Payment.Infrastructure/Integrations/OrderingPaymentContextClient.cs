using UltraSol.Modules.Payment.Application.Contracts;

namespace UltraSol.Modules.Payment.Infrastructure.Integrations;

public sealed class OrderingPaymentContextClient(HttpClient client) : IOrderingPaymentContextClient
{
    public async Task<OrderingPaymentContext> GetAsync(Guid orderId, CancellationToken ct) =>
        await PaymentHttp.SendAsync<OrderingPaymentContext>(client, HttpMethod.Get, $"api/ordering/orders/{orderId}/payment-context", null, false, ct)
        ?? throw PaymentHttp.Unavailable();
}
