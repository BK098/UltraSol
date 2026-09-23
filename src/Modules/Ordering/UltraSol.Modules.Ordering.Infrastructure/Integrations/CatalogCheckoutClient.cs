using UltraSol.Modules.Ordering.Application.Checkout;

namespace UltraSol.Modules.Ordering.Infrastructure.Integrations;

public sealed class CatalogCheckoutClient(HttpClient client) : ICatalogCheckoutClient
{
    public async Task<CatalogItem[]> GetAsync(Guid[] productItemIds, CancellationToken ct)
    {
        var response = await OrderingHttp.SendAsync<CatalogItems>(client, HttpMethod.Post, "api/catalog/checkout/items", new { productItemIds }, false, ct);
        return response?.Items ?? throw OrderingHttp.Unavailable();
    }
}