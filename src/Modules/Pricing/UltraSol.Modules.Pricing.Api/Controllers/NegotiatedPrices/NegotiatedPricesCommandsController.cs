using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Pricing.Application.Features.NegotiatedPrices.Commands;
using UltraSol.Modules.Pricing.Application.Features.NegotiatedPrices.Models;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Pricing.Api.Controllers;

[Authorize]
[Route("api/pricing/negotiated-prices")]
internal sealed class NegotiatedPricesCommandsController(ISender sender) : BaseController(sender)
{
    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateNegotiatedPriceDto? model, CancellationToken cancellationToken) =>
        SendAsync(new CreateNegotiatedPriceCommand(model), cancellationToken);

    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken) =>
        SendAsync(new SubmitNegotiatedPriceCommand(id), cancellationToken);

    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken) =>
        SendAsync(new ApproveNegotiatedPriceCommand(id), cancellationToken);

    [HttpPost("{id:guid}/reject")]
    public Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken) =>
        SendAsync(new RejectNegotiatedPriceCommand(id), cancellationToken);

    [HttpPost("{id:guid}/revoke")]
    public Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken) =>
        SendAsync(new RevokeNegotiatedPriceCommand(id), cancellationToken);
}