using UltraSol.Modules.Pricing.Application.Features.Quotes.Commands;

namespace UltraSol.Modules.Pricing.Application.Features.Quotes.Services;

public abstract class PricingQuoteService
{
    public abstract Task<CreatePriceQuoteCommand.Response> CreateAsync(CreatePriceQuoteCommand.Request request, CancellationToken ct);
}