using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Commands;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Pricing.Api.Controllers;

[Authorize]
[Route("api/pricing/sku-prices")]
internal sealed class SkuPricesCommandsController(ISender sender) : BaseController(sender)
{
    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateSkuPriceDto? model, CancellationToken cancellationToken) =>
        SendAsync(new CreateSkuPriceCommand(model), cancellationToken);

    [HttpPost("{id:guid}/initial-price")]
    public Task<IActionResult> SetInitialPrice(Guid id, [FromBody] InitialPriceDto? model, CancellationToken cancellationToken) =>
        SendAsync(new SetInitialPriceCommand(id, model), cancellationToken);

    [HttpPost("{id:guid}/change-now")]
    public Task<IActionResult> ChangeNow(Guid id, [FromBody] PriceTiersDto? model, CancellationToken cancellationToken) =>
        SendAsync(new ChangePriceImmediatelyCommand(id, model), cancellationToken);

    [HttpPost("{id:guid}/scheduled-prices")]
    public Task<IActionResult> Schedule(Guid id, [FromBody] ScheduledPriceDto? model, CancellationToken cancellationToken) =>
        SendAsync(new SchedulePriceChangeCommand(id, model), cancellationToken);

    [HttpPut("{id:guid}/scheduled-prices/{periodId:guid}/tiers")]
    public Task<IActionResult> ChangeScheduledPrice(Guid id, Guid periodId, [FromBody] PriceTiersDto? model, CancellationToken cancellationToken) =>
        SendAsync(new ChangeScheduledPriceCommand(id, periodId, model), cancellationToken);

    [HttpPut("{id:guid}/scheduled-prices/{periodId:guid}/effective-from")]
    public Task<IActionResult> Reschedule(Guid id, Guid periodId, [FromBody] ReschedulePriceDto? model, CancellationToken cancellationToken) =>
        SendAsync(new ReschedulePriceChangeCommand(id, periodId, model), cancellationToken);

    [HttpDelete("{id:guid}/scheduled-prices/{periodId:guid}")]
    public Task<IActionResult> Cancel(Guid id, Guid periodId, CancellationToken cancellationToken) =>
        SendAsync(new CancelScheduledPriceCommand(id, periodId), cancellationToken);

    [HttpPost("{id:guid}/amendments/price-change")]
    public Task<IActionResult> ProposePriceChange(Guid id, [FromBody] ProposePriceChangeDto? model, CancellationToken cancellationToken) =>
        SendAsync(new ProposeContractPriceChangeCommand(id, model), cancellationToken);

    [HttpPost("{id:guid}/amendments/scheduled-price-change")]
    public Task<IActionResult> ProposeScheduledPriceChange(Guid id, [FromBody] ProposeScheduledPriceChangeDto? model, CancellationToken cancellationToken) =>
        SendAsync(new ProposeContractScheduledPriceChangeCommand(id, model), cancellationToken);

    [HttpPost("{id:guid}/amendments/reschedule")]
    public Task<IActionResult> ProposeReschedule(Guid id, [FromBody] ProposePriceRescheduleDto? model, CancellationToken cancellationToken) =>
        SendAsync(new ProposeContractPriceRescheduleCommand(id, model), cancellationToken);

    [HttpPost("{id:guid}/amendments/cancellation")]
    public Task<IActionResult> ProposeCancellation(Guid id, [FromBody] ProposePriceCancellationDto? model, CancellationToken cancellationToken) =>
        SendAsync(new ProposeContractPriceCancellationCommand(id, model), cancellationToken);

    [HttpPost("{id:guid}/amendments/{amendmentId:guid}/approve")]
    public Task<IActionResult> ApproveAmendment(Guid id, Guid amendmentId, CancellationToken cancellationToken) =>
        SendAsync(new ApproveContractPriceAmendmentCommand(id, amendmentId), cancellationToken);

    [HttpPost("{id:guid}/amendments/{amendmentId:guid}/reject")]
    public Task<IActionResult> RejectAmendment(Guid id, Guid amendmentId, CancellationToken cancellationToken) =>
        SendAsync(new RejectContractPriceAmendmentCommand(id, amendmentId), cancellationToken);
}