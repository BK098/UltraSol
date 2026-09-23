using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Catalog.Application.Features.Checkout.Queries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Catalog.Api.Controllers;

[Authorize]
[Route("api/catalog/checkout")]
internal sealed class CheckoutController(ISender sender) : BaseController(sender)
{
    [HttpPost("items")]
    [ProducesResponseType(typeof(ApiResult<GetCheckoutItemsQuery.Response>), 200)]
    public Task<IActionResult> Items([FromBody] GetCheckoutItemsRequest? request, CancellationToken cancellationToken) =>
        SendAsync(new GetCheckoutItemsQuery(request?.ProductItemIds), cancellationToken);
}

public sealed record GetCheckoutItemsRequest(Guid[]? ProductItemIds);