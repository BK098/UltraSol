using UltraSol.Modules.Ordering.Application.Checkout;

namespace UltraSol.Modules.Ordering.Infrastructure.Integrations;

public sealed class InventoryReservationClient(HttpClient client) : IInventoryReservationClient
{
    public async Task<Reservation> ReserveAsync(ReserveRequest request, CancellationToken ct) =>
        await OrderingHttp.SendAsync<Reservation>(client, HttpMethod.Post, "api/inventory/reservations", request, false, ct) ?? throw OrderingHttp.Unavailable();
    public Task<Reservation?> FindAsync(Guid orderId, CancellationToken ct) =>
        OrderingHttp.SendAsync<Reservation>(client, HttpMethod.Get, $"api/inventory/reservations/by-order/{orderId:D}", null, true, ct);
    public async Task<Reservation> ReleaseAsync(Guid orderId, CancellationToken ct) =>
        await OrderingHttp.SendAsync<Reservation>(client, HttpMethod.Post, $"api/inventory/reservations/by-order/{orderId:D}/release", null, false, ct) ?? throw OrderingHttp.Unavailable();
}