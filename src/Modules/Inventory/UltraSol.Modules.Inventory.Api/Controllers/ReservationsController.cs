using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Modules.Inventory.Application.Features.Reservations.Commands;
using UltraSol.Modules.Inventory.Application.Features.Reservations.Queries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Inventory.Api.Controllers;

[Route("api/inventory/reservations")]
internal sealed class ReservationsController(ISender sender) : BaseController(sender)
{
    [HttpPost]
    [ProducesResponseType(typeof(ApiResult<ReservationResult>), 201)]
    public Task<IActionResult> Reserve([FromBody] ReserveStockRequest? model, CancellationToken ct) =>
        SendAsync(new ReserveStockCommand(model, ActorId()), ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResult<GetReservationQuery.Response>), 200)]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => SendAsync(new GetReservationQuery(id), ct);

    [HttpGet("by-order/{orderId:guid}")]
    [ProducesResponseType(typeof(ApiResult<GetReservationQuery.Response>), 200)]
    public Task<IActionResult> GetByOrder(Guid orderId, CancellationToken ct) => SendAsync(new GetReservationByOrderQuery(orderId), ct);

    [HttpPost("{id:guid}/release")]
    [ProducesResponseType(typeof(ApiResult<ReservationResult>), 200)]
    public Task<IActionResult> Release(Guid id, CancellationToken ct) => SendAsync(new ReleaseReservationCommand(id, ActorId()), ct);

    [HttpPost("by-order/{orderId:guid}/release")]
    [ProducesResponseType(typeof(ApiResult<ReservationResult>), 200)]
    public Task<IActionResult> ReleaseByOrder(Guid orderId, CancellationToken ct) =>
        SendAsync(new ReleaseReservationByOrderCommand(orderId, ActorId()), ct);

    [HttpPost("{id:guid}/issue")]
    [ProducesResponseType(typeof(ApiResult<ReservationResult>), 200)]
    public Task<IActionResult> Issue(Guid id, CancellationToken ct) => SendAsync(new IssueReservationCommand(id, ActorId()), ct);

    private string? ActorId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
}
