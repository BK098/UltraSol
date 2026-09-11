using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Catalog.Application.Features.Categories.Commands;
using UltraSol.Shared.Infrastructure.Api;
using UltraSol.Modules.Catalog.Application.Features.Categories.Queries;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Catalog.Api.Controllers;

[Route("api/categories")]
internal class CategoriesController(ISender sender) : BaseController(sender)
{
    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateCategoryDto model, CancellationToken cancellationToken) =>
        SendAsync(new CreateCategoryCommand(model), cancellationToken);

    [HttpGet("/api/categories")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<PaginatedResult<GetCategoriesQuery.Response>>), 200)]
    public Task<IActionResult> GetCategories([FromQuery] PagedFilter filter, [FromQuery] Guid? parentId, [FromQuery] bool rootsOnly, [FromQuery] bool? isArchived, CancellationToken cancellationToken) =>
        SendAsync(new GetCategoriesQuery(filter, parentId, rootsOnly, isArchived), cancellationToken);

    [HttpGet("/api/categories/{categoryId:guid}")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<GetCategoryDetailQuery.Response>), 200)]
    public Task<IActionResult> GetCategoryDetail([FromRoute] Guid categoryId, CancellationToken cancellationToken) =>
        SendAsync(new GetCategoryDetailQuery(categoryId), cancellationToken);

    [HttpGet("/api/categories/{categoryId:guid}/products")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<PaginatedResult<GetCategoryProductsQuery.Response>>), 200)]
    public Task<IActionResult> GetCategoryProducts([FromRoute] Guid categoryId, [FromQuery] PagedFilter filter, [FromQuery] ProductStatus? status, CancellationToken cancellationToken) =>
        SendAsync(new GetCategoryProductsQuery(categoryId, filter, status), cancellationToken);

    [HttpGet("/api/categories/{categoryId:guid}/children")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<PaginatedResult<GetCategoryChildrenQuery.Response>>), 200)]
    public Task<IActionResult> GetCategoryChildren([FromRoute] Guid categoryId, [FromQuery] PagedFilter filter, [FromQuery] bool? isArchived, CancellationToken cancellationToken) =>
        SendAsync(new GetCategoryChildrenQuery(categoryId, filter, isArchived), cancellationToken);
}