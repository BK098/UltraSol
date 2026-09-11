using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Catalog.Application.Features.Collections.Commands;
using UltraSol.Shared.Infrastructure.Api;
using UltraSol.Modules.Catalog.Application.Features.Collections.Queries;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Catalog.Api.Controllers;

[Route("api/collections")]
internal sealed class CollectionsController(ISender sender) : BaseController(sender)
{
    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateCollectionDto? model, CancellationToken cancellationToken) =>
        SendAsync(new CreateCollectionCommand(model), cancellationToken);

    [HttpPut("{collectionId:guid}/name")]
    public Task<IActionResult> Rename(Guid collectionId, [FromBody] RenameCollectionDto? model, CancellationToken cancellationToken) =>
        SendAsync(new RenameCollectionCommand(collectionId, model), cancellationToken);

    [HttpPost("{collectionId:guid}/publish")]
    public Task<IActionResult> Publish(Guid collectionId, CancellationToken cancellationToken) =>
        SendAsync(new PublishCollectionCommand(collectionId), cancellationToken);

    [HttpPost("{collectionId:guid}/unpublish")]
    public Task<IActionResult> Unpublish(Guid collectionId, CancellationToken cancellationToken) =>
        SendAsync(new UnpublishCollectionCommand(collectionId), cancellationToken);

    [HttpPost("{collectionId:guid}/archive")]
    public Task<IActionResult> Archive(Guid collectionId, CancellationToken cancellationToken) =>
        SendAsync(new ArchiveCollectionCommand(collectionId), cancellationToken);

    [HttpPost("{collectionId:guid}/products/{productId:guid}")]
    public Task<IActionResult> AddProduct(Guid collectionId, Guid productId, CancellationToken cancellationToken) =>
        SendAsync(new AddProductToCollectionCommand(collectionId, productId), cancellationToken);

    [HttpDelete("{collectionId:guid}/products/{productId:guid}")]
    public Task<IActionResult> RemoveProduct(Guid collectionId, Guid productId, CancellationToken cancellationToken) =>
        SendAsync(new RemoveProductFromCollectionCommand(collectionId, productId), cancellationToken);

    [HttpPut("{collectionId:guid}/products/order")]
    public Task<IActionResult> ReorderProducts(Guid collectionId, [FromBody] ReorderCollectionProductsDto? model, CancellationToken cancellationToken) =>
        SendAsync(new ReorderCollectionProductsCommand(collectionId, model), cancellationToken);

    [HttpGet("/api/collections")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<PaginatedResult<GetCollectionsQuery.Response>>), 200)]
    public Task<IActionResult> GetCollections([FromQuery] PagedFilter filter, [FromQuery] CollectionStatus? status, [FromQuery] CollectionType? type, CancellationToken cancellationToken) =>
        SendAsync(new GetCollectionsQuery(filter, status, type), cancellationToken);

    [HttpGet("/api/collections/{collectionId:guid}")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<GetCollectionDetailQuery.Response>), 200)]
    public Task<IActionResult> GetCollectionDetail([FromRoute] Guid collectionId, CancellationToken cancellationToken) =>
        SendAsync(new GetCollectionDetailQuery(collectionId), cancellationToken);

    [HttpGet("/api/collections/{collectionId:guid}/products")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<PaginatedResult<GetCollectionProductsQuery.Response>>), 200)]
    public Task<IActionResult> GetCollectionProducts([FromRoute] Guid collectionId, [FromQuery] PagedFilter filter, [FromQuery] ProductStatus? status, CancellationToken cancellationToken) =>
        SendAsync(new GetCollectionProductsQuery(collectionId, filter, status), cancellationToken);
}