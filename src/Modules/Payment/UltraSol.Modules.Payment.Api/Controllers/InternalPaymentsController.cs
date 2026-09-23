using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Payment.Application.Features.Internal;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Payment.Api.Controllers;

[Route("api/payments/internal/orders/{id:guid}")]
internal sealed class InternalPaymentsController(ISender sender) : BaseController(sender)
{
    public sealed record SessionRequest(string IpAddress);

    [HttpGet]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => SendAsync(new GetOrderPaymentQuery(id), ct);

    [HttpPost("vnpay-sessions")]
    public Task<IActionResult> Create(Guid id, SessionRequest body, CancellationToken ct) =>
        SendAsync(new CreateVnpaySessionCommand(id, Request.Headers["Idempotency-Key"].ToString(), body.IpAddress), ct);
}
