using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Checkout.Commands;
using UltraSol.Modules.Ordering.Application.Features.Orders.Commands;
using UltraSol.Modules.Ordering.Application.Features.Orders.Queries;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Ordering.Api.Controllers;

[Route("api/ordering/admin/orders")]
internal sealed class AdminOrdersController(ISender sender) : BaseController(sender)
{
    public sealed record NoteRequest(string ConcurrencyStamp, string Text);

    [HttpGet]
    public Task<IActionResult> List([FromQuery] PagedFilter filter, CancellationToken ct) => SendAsync(new ListOrdersQuery(filter), ct);

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => SendAsync(new GetAdminOrderQuery(id), ct);

    [HttpPost("assisted")]
    public Task<IActionResult> Create(AssistedOrderRequest? model, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        SendAsync(new CreateAssistedOrderCommand(key, model), ct);

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, OrdersController.CancelRequest model, CancellationToken ct) =>
        SendAsync(new AdminCancelOrderCommand(id, model.ConcurrencyStamp, model.Reason), ct);

    [HttpPost("{id:guid}/notes")]
    public Task<IActionResult> Note(Guid id, NoteRequest model, CancellationToken ct) =>
        SendAsync(new AddOrderNoteCommand(id, model.ConcurrencyStamp, model.Text), ct);
}