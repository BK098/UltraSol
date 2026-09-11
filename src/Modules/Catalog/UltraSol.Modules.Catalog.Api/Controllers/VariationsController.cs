using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Catalog.Application.Features.Variations.Commands;
using UltraSol.Modules.Catalog.Application.Features.Variations.Queries;
using UltraSol.Shared.Infrastructure.Api;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Catalog.Api.Controllers;

[Route("api/products/{productId:guid}/variations")]
internal sealed class VariationsController(ISender sender) : BaseController(sender)
{
    [HttpPost]
    public Task<IActionResult> Add(Guid productId, [FromBody] AddVariationDto? model, CancellationToken cancellationToken) =>
        SendAsync(new AddVariationCommand(productId, model), cancellationToken);

    [HttpPut("{variationId:guid}")]
    public Task<IActionResult> Rename(Guid productId, Guid variationId, [FromBody] RenameVariationDto? model, CancellationToken cancellationToken) =>
        SendAsync(new RenameVariationCommand(productId, variationId, model), cancellationToken);

    [HttpDelete("{variationId:guid}")]
    public Task<IActionResult> Remove(Guid productId, Guid variationId, CancellationToken cancellationToken) =>
        SendAsync(new RemoveVariationCommand(productId, variationId), cancellationToken);

    [HttpPost("{variationId:guid}/options")]
    public Task<IActionResult> AddOption(Guid productId, Guid variationId, [FromBody] AddVariationOptionDto? model, CancellationToken cancellationToken) =>
        SendAsync(new AddVariationOptionCommand(productId, variationId, model), cancellationToken);

    [HttpPut("{variationId:guid}/options/{optionId:guid}")]
    public Task<IActionResult> RenameOption(Guid productId, Guid variationId, Guid optionId, [FromBody] RenameVariationOptionDto? model, CancellationToken cancellationToken) =>
        SendAsync(new RenameVariationOptionCommand(productId, variationId, optionId, model), cancellationToken);

    [HttpDelete("{variationId:guid}/options/{optionId:guid}")]
    public Task<IActionResult> RemoveOption(Guid productId, Guid variationId, Guid optionId, CancellationToken cancellationToken) =>
        SendAsync(new RemoveVariationOptionCommand(productId, variationId, optionId), cancellationToken);

    [HttpGet("/api/products/{productId:guid}/variations")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<PaginatedResult<GetVariationsQuery.Response>>), 200)]
    public Task<IActionResult> GetVariations([FromRoute] Guid productId, [FromQuery] PagedFilter filter, CancellationToken cancellationToken) =>
        SendAsync(new GetVariationsQuery(productId, filter), cancellationToken);

    [HttpGet("/api/products/{productId:guid}/variations/{variationId:guid}")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<GetVariationDetailQuery.Response>), 200)]
    public Task<IActionResult> GetVariationDetail([FromRoute] Guid productId, [FromRoute] Guid variationId, CancellationToken cancellationToken) =>
        SendAsync(new GetVariationDetailQuery(productId, variationId), cancellationToken);

    [HttpGet("/api/products/{productId:guid}/variations/{variationId:guid}/options")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<PaginatedResult<GetVariationOptionsQuery.Response>>), 200)]
    public Task<IActionResult> GetVariationOptions([FromRoute] Guid productId, [FromRoute] Guid variationId, [FromQuery] PagedFilter filter, CancellationToken cancellationToken) =>
        SendAsync(new GetVariationOptionsQuery(productId, variationId, filter), cancellationToken);

    [HttpGet("/api/products/{productId:guid}/variations/{variationId:guid}/options/{optionId:guid}")]
    [ProducesResponseType(typeof(UltraSol.Shared.Application.Responses.ApiResult<GetVariationOptionDetailQuery.Response>), 200)]
    public Task<IActionResult> GetVariationOptionDetail([FromRoute] Guid productId, [FromRoute] Guid variationId, [FromRoute] Guid optionId, CancellationToken cancellationToken) =>
        SendAsync(new GetVariationOptionDetailQuery(productId, variationId, optionId), cancellationToken);
}