using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Queries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Pricing.Api.Controllers;

[Authorize]
[Route("api/pricing/sku-prices")]
internal sealed class SkuPricesQueriesController(ISender sender) : BaseController(sender)
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<PaginatedResult<GetSkuPricesQuery.Response>>), 200)]
    public Task<IActionResult> GetSkuPrices([FromQuery] PagedFilter filter, [FromQuery] Guid? priceListId, [FromQuery] Guid? skuId, [FromQuery] Guid? contractId, CancellationToken ct) =>
        SendAsync(new GetSkuPricesQuery(filter, priceListId, skuId, contractId), ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResult<GetSkuPriceDetailQuery.Response>), 200)]
    public Task<IActionResult> GetSkuPriceDetail(Guid id, CancellationToken ct) =>
        SendAsync(new GetSkuPriceDetailQuery(id), ct);

    [HttpGet("{id:guid}/periods")]
    [ProducesResponseType(typeof(ApiResult<PaginatedResult<GetPricePeriodsQuery.Response>>), 200)]
    public Task<IActionResult> GetPricePeriods(Guid id, [FromQuery] PagedFilter filter, CancellationToken ct) =>
        SendAsync(new GetPricePeriodsQuery(id, filter), ct);

    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(ApiResult<PaginatedResult<GetPriceHistoryQuery.Response>>), 200)]
    public Task<IActionResult> GetPriceHistory(Guid id, [FromQuery] PagedFilter filter, CancellationToken ct) =>
        SendAsync(new GetPriceHistoryQuery(id, filter), ct);

    [HttpGet("{id:guid}/amendments")]
    [ProducesResponseType(typeof(ApiResult<PaginatedResult<GetContractPriceAmendmentsQuery.Response>>), 200)]
    public Task<IActionResult> GetContractPriceAmendments(Guid id, [FromQuery] PagedFilter filter, CancellationToken ct) =>
        SendAsync(new GetContractPriceAmendmentsQuery(id, filter), ct);

    [HttpGet("{id:guid}/amendments/{amendmentId:guid}")]
    [ProducesResponseType(typeof(ApiResult<GetContractPriceAmendmentDetailQuery.Response>), 200)]
    public Task<IActionResult> GetContractPriceAmendmentDetail(Guid id, Guid amendmentId, CancellationToken ct) =>
        SendAsync(new GetContractPriceAmendmentDetailQuery(id, amendmentId), ct);
}