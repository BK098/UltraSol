using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Pricing.Application.Features.Contracts.Queries;
using UltraSol.Modules.Pricing.Application.Features.NegotiatedPrices.Queries;
using UltraSol.Modules.Pricing.Application.Features.PriceLists.Queries;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Queries;
using UltraSol.Modules.Pricing.Application.Reads;
using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.Negotiations;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Modules.Pricing.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Pricing.Infrastructure.Reads;

public sealed class PricingReadStore(PricingDbContext db) : IPricingReadStore
{
    private static async Task<PaginatedResult<T>> Page<T>(IQueryable<T> query, PagedFilter filter, CancellationToken ct)
    {
        var page = PaginationRequest.Create(filter.PageIndex, filter.PageSize);
        var count = await query.CountAsync(ct);
        var rows = await query.Skip(page.Skip).Take(page.Take).ToListAsync(ct);
        return PaginatedResult<T>.Create(rows, count, page);
    }

    public async Task<PaginatedResult<GetPriceListsQuery.Response>> QueryAsync(GetPriceListsQuery request, CancellationToken ct)
    {
        var query = db.PriceLists.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Filter!.Search))
        {
            var search = request.Filter.Search.Trim().ToLowerInvariant();
            query = query.Where(row => row.Name.ToLower().Contains(search));
        }
        if (request.Type is { } type)
        {
            query = query.Where(row => row.Type == type);
        }
        if (request.Currency is { } code)
        {
            var currency = Currency.Create(code);
            query = query.Where(row => row.Currency == currency);
        }
        var page = await Page(query.OrderBy(row => row.Name).ThenBy(row => row.Id), request.Filter, ct);
        return page.Map(row => new GetPriceListsQuery.Response(row.Id, row.Name, row.Currency.Code, row.Type));
    }

    public async Task<GetPriceListDetailQuery.Response> QueryAsync(GetPriceListDetailQuery request, CancellationToken ct)
    {
        var row = await db.PriceLists.AsNoTracking().SingleOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw EntityNotFoundException.For<PriceList>(request.Id);
        return new(row.Id, row.Name, row.Currency.Code, row.Type);
    }

    public async Task<PaginatedResult<GetSkuPricesQuery.Response>> QueryAsync(GetSkuPricesQuery request, CancellationToken ct)
    {
        var query = db.SkuPrices.AsNoTracking();
        if (request.PriceListId is { } listId)
        {
            query = query.Where(row => row.PriceListId == listId);
        }
        if (request.SkuId is { } skuId)
        {
            query = query.Where(row => row.SkuId == skuId);
        }
        if (request.ContractId is { } contractId)
        {
            query = query.Where(row => row.ContractId == contractId);
        }
        var page = await Page(query.OrderBy(row => row.Id), request.Filter!, ct);
        return page.Map(row => new GetSkuPricesQuery.Response(row.Id, row.PriceListId, row.SkuId, row.Currency.Code, row.Type, row.ContractId));
    }

    public async Task<GetSkuPriceDetailQuery.Response> QueryAsync(GetSkuPriceDetailQuery request, CancellationToken ct)
    {
        var row = await db.SkuPrices.AsNoTracking().SingleOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw EntityNotFoundException.For<SkuPrice>(request.Id);
        return new(row.Id, row.PriceListId, row.SkuId, row.Currency.Code, row.Type, row.ContractId, row.Revision);
    }

    private async Task RequireSkuPrice(Guid id, CancellationToken ct)
    {
        if (!await db.SkuPrices.AsNoTracking().AnyAsync(row => row.Id == id, ct))
        {
            throw EntityNotFoundException.For<SkuPrice>(id);
        }
    }

    public async Task<PaginatedResult<GetPricePeriodsQuery.Response>> QueryAsync(GetPricePeriodsQuery request, CancellationToken ct)
    {
        await RequireSkuPrice(request.Id, ct);
        var page = await Page(db.PricePeriods.AsNoTracking().Where(row => row.SkuPriceId == request.Id)
            .OrderBy(row => row.EffectiveFrom).ThenBy(row => row.Id), request.Filter!, ct);
        var ids = page.Items.Select(row => row.Id).ToArray();
        var tiers = await db.PriceTiers.AsNoTracking().Where(row => ids.Contains(row.PricePeriodId))
            .OrderBy(row => row.MinimumQuantity).ToListAsync(ct);
        var grouped = tiers.ToLookup(row => row.PricePeriodId);
        return page.Map(row => new GetPricePeriodsQuery.Response(row.Id, row.EffectiveFrom, row.EffectiveTo,
            grouped[row.Id].Select(tier => new GetPricePeriodsQuery.Tier(tier.MinimumQuantity, tier.Amount)).ToArray(), row.ContractPriceAmendmentId));
    }

    public async Task<PaginatedResult<GetPriceHistoryQuery.Response>> QueryAsync(GetPriceHistoryQuery request, CancellationToken ct)
    {
        await RequireSkuPrice(request.Id, ct);
        var page = await Page(db.PriceChanges.AsNoTracking().Where(row => row.SkuPriceId == request.Id)
            .OrderByDescending(row => row.Revision), request.Filter!, ct);
        return page.Map(row => new GetPriceHistoryQuery.Response(row.Revision, row.Kind, row.ActorId, row.OccurredAt,
            HistoryPeriods(row.BeforeJson), HistoryPeriods(row.AfterJson), row.ContractPriceAmendmentId));
    }

    private static IReadOnlyList<GetPriceHistoryQuery.Period> HistoryPeriods(string json) => PricingJson.DeserializePeriods(json)
        .Select(row => new GetPriceHistoryQuery.Period(row.Id, row.EffectivePeriod.From, row.EffectivePeriod.To,
            row.Tiers.Select(tier => new GetPriceHistoryQuery.Tier(tier.MinimumQuantity, tier.Amount)).ToArray(), row.ContractPriceAmendmentId)).ToArray();

    public async Task<PaginatedResult<GetContractPriceAmendmentsQuery.Response>> QueryAsync(GetContractPriceAmendmentsQuery request, CancellationToken ct)
    {
        await RequireSkuPrice(request.Id, ct);
        var page = await Page(db.ContractPriceAmendments.AsNoTracking().Where(row => row.SkuPriceId == request.Id)
            .OrderByDescending(row => row.ProposedAt).ThenBy(row => row.Id), request.Filter!, ct);
        return page.Map(stored =>
        {
            var row = PricingJson.DeserializeAmendment(stored);
            return new GetContractPriceAmendmentsQuery.Response(row.Id, row.ContractId, row.PricePeriodId, row.Kind, row.Status, row.EffectiveFrom, row.ProposedAt);
        });
    }

    public async Task<GetContractPriceAmendmentDetailQuery.Response> QueryAsync(GetContractPriceAmendmentDetailQuery request, CancellationToken ct)
    {
        await RequireSkuPrice(request.Id, ct);
        var stored = await db.ContractPriceAmendments.AsNoTracking().SingleOrDefaultAsync(row => row.SkuPriceId == request.Id && row.Id == request.AmendmentId, ct)
            ?? throw EntityNotFoundException.For<ContractPriceAmendment>(request.AmendmentId);
        var row = PricingJson.DeserializeAmendment(stored);
        return new(row.Id, row.ContractId, row.PricePeriodId, row.Kind, row.Status, row.EffectiveFrom,
            row.Tiers.Select(tier => new GetContractPriceAmendmentDetailQuery.Tier(tier.MinimumQuantity, tier.Amount)).ToArray(),
            row.Reason, row.ProposedRevision, row.ProposedBy, row.ProposedAt, row.DecidedBy, row.DecidedAt);
    }

    public async Task<PaginatedResult<GetContractsQuery.Response>> QueryAsync(GetContractsQuery request, CancellationToken ct)
    {
        var query = db.Contracts.AsNoTracking();
        if (request.CustomerId is { } customerId)
        {
            query = query.Where(row => row.CustomerId == customerId);
        }
        if (request.Status is { } status)
        {
            query = query.Where(row => row.Status == status);
        }
        var page = await Page(query.OrderBy(row => row.Id), request.Filter!, ct);
        return page.Map(row => new GetContractsQuery.Response(row.Id, row.CustomerId, row.PriceListId, row.Currency.Code, row.Status, row.Validity.From, row.Validity.To));
    }

    public async Task<GetContractDetailQuery.Response> QueryAsync(GetContractDetailQuery request, CancellationToken ct)
    {
        var row = await db.Contracts.AsNoTracking().SingleOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw EntityNotFoundException.For<Contract>(request.Id);
        return new(row.Id, row.CustomerId, row.PriceListId, row.Currency.Code, row.Status, row.Validity.From, row.Validity.To,
            row.CommercialTerms, row.ActivatedBy, row.ActivatedAt, row.TerminatedBy, row.TerminatedAt);
    }

    public async Task<PaginatedResult<GetNegotiatedPricesQuery.Response>> QueryAsync(GetNegotiatedPricesQuery request, CancellationToken ct)
    {
        var query = db.NegotiatedPrices.AsNoTracking();
        if (request.CustomerId is { } customerId)
        {
            query = query.Where(row => row.CustomerId == customerId);
        }
        if (request.SkuId is { } skuId)
        {
            query = query.Where(row => row.SkuId == skuId);
        }
        if (request.TransactionId is { } transactionId)
        {
            query = query.Where(row => row.TransactionId == transactionId);
        }
        if (request.Channel is { } channel)
        {
            query = query.Where(row => row.Channel == channel);
        }
        if (request.Status is { } status)
        {
            query = query.Where(row => row.Status == status);
        }
        var page = await Page(query.OrderBy(row => row.Id), request.Filter!, ct);
        return page.Map(row => new GetNegotiatedPricesQuery.Response(row.Id, row.CustomerId, row.SkuId, row.TransactionId, row.Quantity, row.Amount,
            row.Currency.Code, row.Channel, row.Status, row.ContractId));
    }

    public async Task<GetNegotiatedPriceDetailQuery.Response> QueryAsync(GetNegotiatedPriceDetailQuery request, CancellationToken ct)
    {
        var row = await db.NegotiatedPrices.AsNoTracking().SingleOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw EntityNotFoundException.For<NegotiatedPrice>(request.Id);
        return new(row.Id, row.CustomerId, row.SkuId, row.TransactionId, row.Quantity, row.Amount, row.Currency.Code, row.Channel, row.Status, row.ContractId,
            row.Reason, row.Validity.From, row.Validity.To, row.ProposedBy, row.ProposedAt, row.SubmittedBy, row.SubmittedAt,
            row.ApprovedBy, row.ApprovedAt, row.RejectedBy, row.RejectedAt, row.RevokedBy, row.RevokedAt);
    }
}