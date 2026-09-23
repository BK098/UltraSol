using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.Prices.Models;
using UltraSol.Modules.Pricing.Application.Features.Prices.Services;
using UltraSol.Modules.Pricing.Application.Features.Quotes.Commands;
using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.Negotiations;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Modules.Pricing.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Exceptions;
using QuoteService = UltraSol.Modules.Pricing.Application.Features.Quotes.Services.PricingQuoteService;

namespace UltraSol.Modules.Pricing.Infrastructure.Quotes;

public sealed class PricingQuoteService(PricingDbContext db, IConfiguration configuration, TimeProvider clock,
    IPriceListRepository priceLists, ISkuPriceRepository skuPrices, IContractRepository contracts,
    INegotiatedPriceRepository negotiatedPrices) : QuoteService
{
    public override async Task<CreatePriceQuoteCommand.Response> CreateAsync(CreatePriceQuoteCommand.Request request, CancellationToken ct)
    {
        var orderType = Enum.Parse<PriceListType>(request.OrderType!, true);
        var currency = Currency.Create(request.Currency!);
        var quotedAt = PricingTime.Normalize(clock.GetUtcNow());
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(() =>
            CreateInSnapshotAsync(request, orderType, currency, quotedAt, ct));
    }

    private async Task<CreatePriceQuoteCommand.Response> CreateInSnapshotAsync(CreatePriceQuoteCommand.Request request,
        PriceListType orderType, Currency currency, DateTimeOffset quotedAt, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        Contract? contract = null;
        Guid priceListId;
        if (orderType == PriceListType.Contract)
        {
            contract = await contracts.GetRequiredByIdAsync(request.ContractId!.Value, ct);
            priceListId = contract.PriceListId;
        }
        else
        {
            var configured = configuration[$"Pricing:DefaultPriceLists:{orderType}:{currency.Code}"];
            if (!Guid.TryParse(configured, out priceListId) || priceListId == Guid.Empty)
            {
                throw new DomainException($"No default {orderType} price list is configured for {currency.Code}.", "DefaultPriceListNotConfigured");
            }
        }
        var priceList = await priceLists.GetRequiredByIdAsync(priceListId, ct);
        var lines = new List<CreatePriceQuoteCommand.QuotedLine>(request.Lines!.Length);
        foreach (var line in request.Lines)
        {
            var negotiated = line.NegotiatedPriceId is { } negotiatedId
                ? await negotiatedPrices.GetRequiredByIdAsync(negotiatedId, ct)
                : null;
            var skuPrice = negotiated is null
                ? await skuPrices.SingleOrDefaultAsync(price => price.PriceListId == priceListId && price.SkuId == line.ProductItemId &&
                    price.ContractId == request.ContractId, ct)
                : null;
            var resolved = PriceResolver.Resolve(new PriceResolutionRequest(line.ProductItemId, line.Quantity, currency, orderType,
                quotedAt, request.CustomerId, request.NegotiationTransactionRef, request.ContractId, line.NegotiatedPriceId),
                priceList, skuPrice, negotiated, contract);
            var total = resolved.UnitPrice * line.Quantity;
            var source = resolved.NegotiatedPriceId is not null ? "Negotiated"
                : resolved.ContractId is not null ? "Contract" : "PriceList";
            lines.Add(new(line.ProductItemId, line.Quantity, resolved.UnitPrice, resolved.UnitPrice, 0, total, source,
                resolved.PriceListId, resolved.SkuPriceId, resolved.PricePeriodId, resolved.ContractPriceAmendmentId,
                resolved.NegotiatedPriceId, resolved.ContractId));
        }
        await transaction.CommitAsync(ct);
        var subtotal = lines.Sum(line => line.LineTotal);
        return new(Guid.NewGuid(), quotedAt, currency.Code, lines, subtotal, 0, 0, 0, subtotal);
    }
}