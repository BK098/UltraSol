using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Ordering.Application.Features.Checkout.Queries;
using UltraSol.Modules.Ordering.Application.Features.Orders.Commands;
using UltraSol.Modules.Ordering.Application.Features.Orders.Queries;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Ordering.Api.Controllers;

[Route("api/ordering")]
internal sealed class OrdersController(ISender sender) : BaseController(sender)
{
    public sealed record CancelRequest(string ConcurrencyStamp, string Reason);

    [HttpGet("checkouts/{id:guid}")]
    public Task<IActionResult> Checkout(Guid id, CancellationToken ct) => SendAsync(new GetCheckoutQuery(id, GuestToken()), ct);

    [HttpGet("orders/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => SendAsync(new GetOrderQuery(id, GuestToken()), ct);

    [HttpGet("orders/mine")]
    public Task<IActionResult> Mine([FromQuery] PagedFilter filter, CancellationToken ct) => SendAsync(new GetMyOrdersQuery(filter), ct);

    [HttpPost("orders/{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancelRequest model, CancellationToken ct) =>
        SendAsync(new CancelOrderCommand(id, model.ConcurrencyStamp, model.Reason, GuestToken()), ct);

    private string? GuestToken() => Request.Headers["X-Ordering-Guest-Token"].FirstOrDefault();
}