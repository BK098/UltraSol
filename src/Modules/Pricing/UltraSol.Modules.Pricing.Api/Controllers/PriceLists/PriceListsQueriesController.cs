using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Pricing.Application.Features.PriceLists.Queries;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Pricing.Api.Controllers.PriceLists;

[Authorize]
[Route("api/pricing/price-lists")]
internal sealed class PriceListsQueriesController(ISender sender) : BaseController(sender)
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<PaginatedResult<GetPriceListsQuery.Response>>), 200)]
    public Task<IActionResult> GetPriceLists([FromQuery] PagedFilter filter, [FromQuery] PriceListType? type, [FromQuery] string? currency, CancellationToken ct) =>
        SendAsync(new GetPriceListsQuery(filter, type, currency), ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResult<GetPriceListDetailQuery.Response>), 200)]
    public Task<IActionResult> GetPriceListDetail(Guid id, CancellationToken ct) =>
        SendAsync(new GetPriceListDetailQuery(id), ct);
}