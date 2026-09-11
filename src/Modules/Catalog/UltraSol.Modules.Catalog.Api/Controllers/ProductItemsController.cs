using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Catalog.Application.Features.ProductItems.Commands;
using UltraSol.Modules.Catalog.Application.Features.ProductItems.Queries;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Infrastructure.Api;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;

namespace UltraSol.Modules.Catalog.Api.Controllers;

[Route("api/products/{productId:guid}/items")]
internal sealed class ProductItemsController(ISender sender) : BaseController(sender)
{
    [HttpPost]
    public Task<IActionResult> Create(Guid productId, [FromBody] CreateProductItemDto? model, CancellationToken cancellationToken) =>
        SendAsync(new CreateProductItemCommand(productId, model), cancellationToken);

    [HttpPost("{productItemId:guid}/activate")]
    public Task<IActionResult> Activate(Guid productItemId, CancellationToken cancellationToken) =>
        SendAsync(new ActivateProductItemCommand(productItemId), cancellationToken);

    [HttpPost("{productItemId:guid}/deactivate")]
    public Task<IActionResult> Deactivate(Guid productItemId, CancellationToken cancellationToken) =>
        SendAsync(new DeactivateProductItemCommand(productItemId), cancellationToken);

    [HttpPost("{productItemId:guid}/archive")]
    public Task<IActionResult> Archive(Guid productItemId, CancellationToken cancellationToken) =>
        SendAsync(new ArchiveProductItemCommand(productItemId), cancellationToken);

    [HttpPut("{productItemId:guid}/sku")]
    public Task<IActionResult> ChangeSku(Guid productItemId, [FromBody] ChangeSkuDto? model, CancellationToken cancellationToken) =>
        SendAsync(new ChangeSkuCommand(productItemId, model), cancellationToken);

    [HttpPost("{productItemId:guid}/media")]
    public Task<IActionResult> AddMedia(Guid productId, Guid productItemId, [FromBody] AddProductItemMediaDto? model, CancellationToken cancellationToken) =>
        SendAsync(new AddProductItemMediaCommand(productId, productItemId, model), cancellationToken);

    [HttpDelete("{productItemId:guid}/media/{mediaId:guid}")]
    public Task<IActionResult> RemoveMedia(Guid productId, Guid productItemId, Guid mediaId, CancellationToken cancellationToken) =>
        SendAsync(new RemoveProductItemMediaCommand(productId, productItemId, mediaId), cancellationToken);

    [HttpPut("{productItemId:guid}/media/order")]
    public Task<IActionResult> ReorderMedia(Guid productId, Guid productItemId, [FromBody] ReorderProductMediaDto? model, CancellationToken cancellationToken) =>
        SendAsync(new ReorderProductMediaCommand(productId, productItemId, model), cancellationToken);

    [HttpGet("/api/products/{productId:guid}/items")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<PaginatedResult<GetProductItemsQuery.Response>>), 200)]
    public Task<IActionResult> GetProductItems([FromRoute] Guid productId, [FromQuery] PagedFilter filter, [FromQuery] ProductItemStatus? status, [FromQuery] bool? isBundle, CancellationToken cancellationToken) =>
        SendAsync(new GetProductItemsQuery(productId, filter, status, isBundle), cancellationToken);

    [HttpGet("/api/product-items")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<PaginatedResult<GetAllProductItemsQuery.Response>>), 200)]
    public Task<IActionResult> GetAllProductItems([FromQuery] PagedFilter filter, [FromQuery] ProductItemStatus? status, [FromQuery] bool? isBundle, CancellationToken cancellationToken) =>
        SendAsync(new GetAllProductItemsQuery(filter, status, isBundle), cancellationToken);

    [HttpGet("/api/products/{productId:guid}/items/{productItemId:guid}")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<GetProductItemDetailQuery.Response>), 200)]
    public Task<IActionResult> GetProductItemDetail([FromRoute] Guid productId, [FromRoute] Guid productItemId, CancellationToken cancellationToken) =>
        SendAsync(new GetProductItemDetailQuery(productId, productItemId), cancellationToken);

    [HttpGet("/api/products/{productId:guid}/items/{productItemId:guid}/media")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<PaginatedResult<GetProductItemMediaQuery.Response>>), 200)]
    public Task<IActionResult> GetProductItemMedia([FromRoute] Guid productId, [FromRoute] Guid productItemId, [FromQuery] PagedFilter filter, CancellationToken cancellationToken) =>
        SendAsync(new GetProductItemMediaQuery(productId, productItemId, filter), cancellationToken);

    [HttpGet("/api/products/{productId:guid}/items/{productItemId:guid}/media/{mediaId:guid}")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<GetProductItemMediaDetailQuery.Response>), 200)]
    public Task<IActionResult> GetProductItemMediaDetail([FromRoute] Guid productId, [FromRoute] Guid productItemId, [FromRoute] Guid mediaId, CancellationToken cancellationToken) =>
        SendAsync(new GetProductItemMediaDetailQuery(productId, productItemId, mediaId), cancellationToken);
}