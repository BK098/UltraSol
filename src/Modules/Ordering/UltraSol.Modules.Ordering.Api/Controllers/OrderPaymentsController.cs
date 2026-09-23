using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Ordering.Application.Features.Payments;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Ordering.Api.Controllers;

[Route("api/ordering/orders/{id:guid}")]
internal sealed class OrderPaymentsController(ISender sender) : BaseController(sender)
{
    [HttpGet("payment")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => SendAsync(new GetOrderPaymentQuery(id, GuestToken()), ct);

    [HttpPost("payment/vnpay-sessions")]
    public Task<IActionResult> Session(Guid id, CancellationToken ct) => SendAsync(new CreateOrderVnpaySessionCommand(id, GuestToken(),
        Request.Headers["Idempotency-Key"].ToString(), HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1"), ct);

    [HttpGet("payment-context")]
    public Task<IActionResult> Context(Guid id, CancellationToken ct) => SendAsync(new GetPaymentContextQuery(id), ct);

    private string? GuestToken() => Request.Headers["X-Ordering-Guest-Token"].FirstOrDefault();
}
