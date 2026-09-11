using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Catalog.Application.Features.Bundles.Commands;
using UltraSol.Shared.Infrastructure.Api;
using UltraSol.Modules.Catalog.Application.Features.Bundles.Queries;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Catalog.Api.Controllers;

[Route("api/products/{productId:guid}/items/{productItemId:guid}/bundle/components")]
internal sealed class BundlesController(ISender sender) : BaseController(sender)
{
    [HttpPost("{componentItemId:guid}")]
    public Task<IActionResult> Add(Guid productId, Guid productItemId, Guid componentItemId, [FromBody] AddBundleComponentDto? model, CancellationToken cancellationToken) =>
        SendAsync(new AddBundleComponentCommand(productId, productItemId, componentItemId, model), cancellationToken);

    [HttpPut("{componentItemId:guid}/quantity")]
    public Task<IActionResult> ChangeQuantity(Guid productId, Guid productItemId, Guid componentItemId, [FromBody] ChangeBundleComponentQuantityDto? model, CancellationToken cancellationToken) =>
        SendAsync(new ChangeBundleComponentQuantityCommand(productId, productItemId, componentItemId, model), cancellationToken);

    [HttpDelete("{componentItemId:guid}")]
    public Task<IActionResult> Remove(Guid productId, Guid productItemId, Guid componentItemId, CancellationToken cancellationToken) =>
        SendAsync(new RemoveBundleComponentCommand(productId, productItemId, componentItemId), cancellationToken);

    [HttpGet("/api/bundles")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<PaginatedResult<GetBundlesQuery.Response>>), 200)]
    public Task<IActionResult> GetBundles([FromQuery] PagedFilter filter, [FromQuery] ProductItemStatus? status, [FromQuery] Guid? productId, CancellationToken cancellationToken) =>
        SendAsync(new GetBundlesQuery(filter, status, productId), cancellationToken);

    [HttpGet("/api/products/{productId:guid}/items/{productItemId:guid}/bundle")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<GetBundleDetailQuery.Response>), 200)]
    public Task<IActionResult> GetBundleDetail([FromRoute] Guid productId, [FromRoute] Guid productItemId, CancellationToken cancellationToken) =>
        SendAsync(new GetBundleDetailQuery(productId, productItemId), cancellationToken);

    [HttpGet("/api/products/{productId:guid}/items/{productItemId:guid}/bundle/components")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<PaginatedResult<GetBundleComponentsQuery.Response>>), 200)]
    public Task<IActionResult> GetBundleComponents([FromRoute] Guid productId, [FromRoute] Guid productItemId, [FromQuery] PagedFilter filter, CancellationToken cancellationToken) =>
        SendAsync(new GetBundleComponentsQuery(productId, productItemId, filter), cancellationToken);
}