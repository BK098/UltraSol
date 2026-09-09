using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Catalog.Application.Features.Products.Commands;
using UltraSol.Shared.Infrastructure.Api;

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
}