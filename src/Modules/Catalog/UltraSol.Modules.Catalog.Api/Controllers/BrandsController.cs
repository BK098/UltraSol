using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Catalog.Application.Features.Brands.Commands;
using UltraSol.Shared.Infrastructure.Api;
using UltraSol.Modules.Catalog.Application.Features.Brands.Queries;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Catalog.Api.Controllers;

[Route("api/brands")]
internal sealed class BrandsController(ISender sender) : BaseController(sender)
{
    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateBrandDto? model, CancellationToken cancellationToken) =>
        SendAsync(new CreateBrandCommand(model), cancellationToken);

    [HttpPut("{brandId:guid}/name")]
    public Task<IActionResult> Rename(Guid brandId, [FromBody] RenameBrandDto? model, CancellationToken cancellationToken) =>
        SendAsync(new RenameBrandCommand(brandId, model), cancellationToken);

    [HttpPut("{brandId:guid}/logo")]
    public Task<IActionResult> UpdateLogo(Guid brandId, [FromBody] UpdateBrandLogoDto? model, CancellationToken cancellationToken) =>
        SendAsync(new UpdateBrandLogoCommand(brandId, model), cancellationToken);

    [HttpPost("{brandId:guid}/activate")]
    public Task<IActionResult> Activate(Guid brandId, CancellationToken cancellationToken) =>
        SendAsync(new ActivateBrandCommand(brandId), cancellationToken);

    [HttpPost("{brandId:guid}/deactivate")]
    public Task<IActionResult> Deactivate(Guid brandId, CancellationToken cancellationToken) =>
        SendAsync(new DeactivateBrandCommand(brandId), cancellationToken);

    [HttpPost("{brandId:guid}/archive")]
    public Task<IActionResult> Archive(Guid brandId, CancellationToken cancellationToken) =>
        SendAsync(new ArchiveBrandCommand(brandId), cancellationToken);

    [HttpGet("/api/brands")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<PaginatedResult<GetBrandsQuery.Response>>), 200)]
    public Task<IActionResult> GetBrands([FromQuery] PagedFilter filter, [FromQuery] bool? isActive, [FromQuery] bool? isArchived, CancellationToken cancellationToken) =>
        SendAsync(new GetBrandsQuery(filter, isActive, isArchived), cancellationToken);

    [HttpGet("/api/brands/{brandId:guid}")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<GetBrandDetailQuery.Response>), 200)]
    public Task<IActionResult> GetBrandDetail([FromRoute] Guid brandId, CancellationToken cancellationToken) =>
        SendAsync(new GetBrandDetailQuery(brandId), cancellationToken);

    [HttpGet("/api/brands/{brandId:guid}/products")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<PaginatedResult<GetBrandProductsQuery.Response>>), 200)]
    public Task<IActionResult> GetBrandProducts([FromRoute] Guid brandId, [FromQuery] PagedFilter filter, [FromQuery] ProductStatus? status, CancellationToken cancellationToken) =>
        SendAsync(new GetBrandProductsQuery(brandId, filter, status), cancellationToken);
}