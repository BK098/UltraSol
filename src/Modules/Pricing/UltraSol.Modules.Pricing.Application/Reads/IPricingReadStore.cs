using UltraSol.Modules.Pricing.Application.Features.Contracts.Queries;
using UltraSol.Modules.Pricing.Application.Features.NegotiatedPrices.Queries;
using UltraSol.Modules.Pricing.Application.Features.PriceLists.Queries;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Queries;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Pricing.Application.Reads;

public interface IPricingReadStore
{
    Task<PaginatedResult<GetPriceListsQuery.Response>> QueryAsync(GetPriceListsQuery request, CancellationToken ct);
    Task<GetPriceListDetailQuery.Response> QueryAsync(GetPriceListDetailQuery request, CancellationToken ct);
    Task<PaginatedResult<GetSkuPricesQuery.Response>> QueryAsync(GetSkuPricesQuery request, CancellationToken ct);
    Task<GetSkuPriceDetailQuery.Response> QueryAsync(GetSkuPriceDetailQuery request, CancellationToken ct);
    Task<PaginatedResult<GetPricePeriodsQuery.Response>> QueryAsync(GetPricePeriodsQuery request, CancellationToken ct);
    Task<PaginatedResult<GetPriceHistoryQuery.Response>> QueryAsync(GetPriceHistoryQuery request, CancellationToken ct);
    Task<PaginatedResult<GetContractPriceAmendmentsQuery.Response>> QueryAsync(GetContractPriceAmendmentsQuery request, CancellationToken ct);
    Task<GetContractPriceAmendmentDetailQuery.Response> QueryAsync(GetContractPriceAmendmentDetailQuery request, CancellationToken ct);
    Task<PaginatedResult<GetContractsQuery.Response>> QueryAsync(GetContractsQuery request, CancellationToken ct);
    Task<GetContractDetailQuery.Response> QueryAsync(GetContractDetailQuery request, CancellationToken ct);
    Task<PaginatedResult<GetNegotiatedPricesQuery.Response>> QueryAsync(GetNegotiatedPricesQuery request, CancellationToken ct);
    Task<GetNegotiatedPriceDetailQuery.Response> QueryAsync(GetNegotiatedPriceDetailQuery request, CancellationToken ct);
}