using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Modules.Inventory.Application.Features.Adjustments.Commands;
using UltraSol.Modules.Inventory.Application.Features.Adjustments.Queries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Inventory.Api.Controllers;

[Route("api/inventory/adjustments")]
internal sealed class AdjustmentsController(ISender sender) : BaseController(sender)
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<PaginatedResult<GetAdjustmentsQuery.Response>>), 200)]
    public Task<IActionResult> GetAdjustments([FromQuery] PagedFilter? filter, [FromQuery] string? status, CancellationToken ct) =>
        SendAsync(new GetAdjustmentsQuery(filter, status), ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResult<GetAdjustmentQuery.Response>), 200)]
    public Task<IActionResult> GetAdjustment(Guid id, CancellationToken ct) => SendAsync(new GetAdjustmentQuery(id), ct);

    [HttpPost]
    [ProducesResponseType(typeof(ApiResult<AdjustmentResult>), 201)]
    public Task<IActionResult> Create([FromBody] CreateAdjustmentRequest? model, CancellationToken ct) =>
        SendAsync(new CreateAdjustmentCommand(model, ActorId()), ct);

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResult<AdjustmentResult>), 200)]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateAdjustmentRequest? model, CancellationToken ct) =>
        SendAsync(new UpdateAdjustmentCommand(id, model, ActorId()), ct);

    [HttpPost("{id:guid}/post")]
    [ProducesResponseType(typeof(ApiResult<AdjustmentResult>), 200)]
    public Task<IActionResult> Post(Guid id, CancellationToken ct) => SendAsync(new PostAdjustmentCommand(id, ActorId()), ct);

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResult<AdjustmentResult>), 200)]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) => SendAsync(new CancelAdjustmentCommand(id, ActorId()), ct);

    private string? ActorId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
}
