using UltraSol.Shared.Domain.Common.Paging;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Catalog.Application.Features.Client.Queries;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Shared.Infrastructure.Api;
namespace UltraSol.Modules.Catalog.Api.Controllers;
[Route("api/client/products")]
internal sealed class ClientCatalogController(ISender sender) : BaseController(sender)
{
    [HttpGet]
    public Task<IActionResult> Products([FromQuery] PagedFilter filter, CancellationToken ct) => SendAsync(new GetClientProductsQuery(filter), ct);
    [HttpGet("{productId:guid}")]
    public Task<IActionResult> Product(Guid productId, CancellationToken ct) => SendAsync(new GetClientProductQuery(productId), ct);
}