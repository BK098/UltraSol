using UltraSol.Modules.Ordering.Application.Checkout;

namespace UltraSol.Modules.Ordering.Infrastructure.Integrations;

public sealed class PricingQuoteClient(HttpClient client) : IPricingQuoteClient
{
    public async Task<PriceQuote> QuoteAsync(QuoteRequest request, CancellationToken ct) =>
        await OrderingHttp.SendAsync<PriceQuote>(client, HttpMethod.Post, "api/pricing/quotes", request, false, ct) ?? throw OrderingHttp.Unavailable();
}