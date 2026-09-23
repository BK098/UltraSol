using UltraSol.Modules.Ordering.Application.Features.Payments;

namespace UltraSol.Modules.Ordering.Infrastructure.Integrations;

public sealed class OrderPaymentClient(HttpClient client) : IOrderPaymentClient
{
    public async Task<OrderPaymentSummary> GetAsync(Guid orderId, CancellationToken ct) =>
        await OrderingHttp.SendAsync<OrderPaymentSummary>(client, HttpMethod.Get, $"api/payments/internal/orders/{orderId}", null, false, ct)
        ?? throw OrderingHttp.Unavailable();

    public async Task<OrderPaymentSession> CreateSessionAsync(Guid orderId, string key, string ipAddress, CancellationToken ct) =>
        await OrderingHttp.SendAsync<OrderPaymentSession>(client, HttpMethod.Post, $"api/payments/internal/orders/{orderId}/vnpay-sessions",
            new { ipAddress }, false, ct, key) ?? throw OrderingHttp.Unavailable();
}
