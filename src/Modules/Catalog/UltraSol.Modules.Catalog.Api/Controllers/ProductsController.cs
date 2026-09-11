using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Catalog.Application.Features.Products.Commands;
using UltraSol.Modules.Catalog.Application.Features.Products.Queries;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Infrastructure.Api;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;

namespace UltraSol.Modules.Catalog.Api.Controllers;

[Route("api/products")]
internal sealed class ProductsController(ISender sender) : BaseController(sender)
{
    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateProductDto? model, CancellationToken cancellationToken) =>
        SendAsync(new CreateProductCommand(model), cancellationToken);

    [HttpPost("{productId:guid}/publish")]
    public Task<IActionResult> Publish(Guid productId, CancellationToken cancellationToken) =>
        SendAsync(new PublishProductCommand(productId), cancellationToken);

    [HttpPost("{productId:guid}/unpublish")]
    public Task<IActionResult> Unpublish(Guid productId, CancellationToken cancellationToken) =>
        SendAsync(new UnpublishProductCommand(productId), cancellationToken);

    [HttpPost("{productId:guid}/discard")]
    public Task<IActionResult> DiscardDraft(Guid productId, CancellationToken cancellationToken) =>
        SendAsync(new DiscardDraftProductCommand(productId), cancellationToken);

    [HttpPut("{productId:guid}/brand")]
    public Task<IActionResult> ChangeBrand(Guid productId, [FromBody] ChangeProductBrandDto model, CancellationToken cancellationToken) =>
        SendAsync(new ChangeProductBrandCommand(productId, model), cancellationToken);

    [HttpPost("{productId:guid}/categories/{categoryId:guid}")]
    public Task<IActionResult> AssignCategory(Guid productId, Guid categoryId, CancellationToken cancellationToken) =>
        SendAsync(new AssignProductCategoryCommand(productId, categoryId), cancellationToken);

    [HttpDelete("{productId:guid}/categories/{categoryId:guid}")]
    public Task<IActionResult> RemoveCategory(Guid productId, Guid categoryId, CancellationToken cancellationToken) =>
        SendAsync(new RemoveProductCategoryCommand(productId, categoryId), cancellationToken);

    [HttpPost("{productId:guid}/media")]
    public Task<IActionResult> AddMedia(Guid productId, [FromBody] AddProductMediaDto? model, CancellationToken cancellationToken) =>
        SendAsync(new AddProductMediaCommand(productId, model), cancellationToken);

    [HttpDelete("{productId:guid}/media/{mediaId:guid}")]
    public Task<IActionResult> RemoveMedia(Guid productId, Guid mediaId, CancellationToken cancellationToken) =>
        SendAsync(new RemoveProductMediaCommand(productId, mediaId), cancellationToken);

    [HttpGet("/api/products")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<PaginatedResult<GetProductsQuery.Response>>), 200)]
    public Task<IActionResult> GetProducts([FromQuery] PagedFilter filter, [FromQuery] ProductStatus? status, [FromQuery] Guid? brandId, [FromQuery] Guid? categoryId, CancellationToken cancellationToken) =>
        SendAsync(new GetProductsQuery(filter, status, brandId, categoryId), cancellationToken);

    [HttpGet("/api/products/{productId:guid}")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<GetProductDetailQuery.Response>), 200)]
    public Task<IActionResult> GetProductDetail([FromRoute] Guid productId, CancellationToken cancellationToken) =>
        SendAsync(new GetProductDetailQuery(productId), cancellationToken);

    [HttpGet("/api/products/{productId:guid}/media")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<PaginatedResult<GetProductMediaQuery.Response>>), 200)]
    public Task<IActionResult> GetProductMedia([FromRoute] Guid productId, [FromQuery] PagedFilter filter, CancellationToken cancellationToken) =>
        SendAsync(new GetProductMediaQuery(productId, filter), cancellationToken);

    [HttpGet("/api/products/{productId:guid}/categories")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<PaginatedResult<GetProductCategoriesQuery.Response>>), 200)]
    public Task<IActionResult> GetProductCategories([FromRoute] Guid productId, [FromQuery] PagedFilter filter, CancellationToken cancellationToken) =>
        SendAsync(new GetProductCategoriesQuery(productId, filter), cancellationToken);

    [HttpGet("/api/products/{productId:guid}/media/{mediaId:guid}")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<GetProductMediaDetailQuery.Response>), 200)]
    public Task<IActionResult> GetProductMediaDetail([FromRoute] Guid productId, [FromRoute] Guid mediaId, CancellationToken cancellationToken) =>
        SendAsync(new GetProductMediaDetailQuery(productId, mediaId), cancellationToken);
}