using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Pricing.Application.Features.Prices.Queries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Pricing.Api.Controllers;

[Authorize]
[Route("api/pricing/resolve")]
internal sealed class PriceResolutionController(ISender sender) : BaseController(sender)
{
    [HttpPost]
    [ProducesResponseType(typeof(ApiResult<ResolvePriceQuery.Response>), 200)]
    public Task<IActionResult> Resolve([FromBody] ResolvePriceDto? model, CancellationToken ct) =>
        SendAsync(new ResolvePriceQuery(model), ct);
}