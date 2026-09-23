using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Catalog.Application.Features.ProductItems.Queries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Catalog.Api.Controllers;

[Authorize]
[Route("api/catalog/skus")]
internal sealed class SkuLookupController(ISender sender) : BaseController(sender)
{
    [HttpGet("{skuId:guid}/exists")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    public Task<IActionResult> Exists(Guid skuId, CancellationToken cancellationToken) =>
        SendAsync(new SkuExistsQuery(skuId), cancellationToken);
}