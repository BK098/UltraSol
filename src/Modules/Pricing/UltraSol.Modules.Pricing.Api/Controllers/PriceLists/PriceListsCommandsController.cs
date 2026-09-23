using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Pricing.Application.Features.PriceLists.Commands;
using UltraSol.Modules.Pricing.Application.Features.PriceLists.Models;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Pricing.Api.Controllers.PriceLists;

[Authorize]
[Route("api/pricing/price-lists")]
internal sealed class PriceListsCommandsController(ISender sender) : BaseController(sender)
{
    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreatePriceListDto? model, CancellationToken cancellationToken) =>
        SendAsync(new CreatePriceListCommand(model), cancellationToken);

    [HttpPut("{id:guid}/name")]
    public Task<IActionResult> Rename(Guid id, [FromBody] RenamePriceListDto? model, CancellationToken cancellationToken) =>
        SendAsync(new RenamePriceListCommand(id, model), cancellationToken);
}