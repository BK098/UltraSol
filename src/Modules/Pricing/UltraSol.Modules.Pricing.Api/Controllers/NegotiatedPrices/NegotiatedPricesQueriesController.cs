using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Pricing.Application.Features.NegotiatedPrices.Queries;
using UltraSol.Modules.Pricing.Domain.Negotiations;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Pricing.Api.Controllers;

[Authorize]
[Route("api/pricing/negotiated-prices")]
internal sealed class NegotiatedPricesQueriesController(ISender sender) : BaseController(sender)
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<PaginatedResult<GetNegotiatedPricesQuery.Response>>), 200)]
    public Task<IActionResult> GetNegotiatedPrices([FromQuery] PagedFilter filter, [FromQuery] Guid? customerId, [FromQuery] Guid? skuId, [FromQuery] Guid? transactionId, [FromQuery] PriceListType? channel, [FromQuery] NegotiatedPriceStatus? status, CancellationToken ct) =>
        SendAsync(new GetNegotiatedPricesQuery(filter, customerId, skuId, transactionId, channel, status), ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResult<GetNegotiatedPriceDetailQuery.Response>), 200)]
    public Task<IActionResult> GetNegotiatedPriceDetail(Guid id, CancellationToken ct) =>
        SendAsync(new GetNegotiatedPriceDetailQuery(id), ct);
}