using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Pricing.Application.Features.Quotes.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Pricing.Api.Controllers.Quotes;

[Authorize]
[Route("api/pricing/quotes")]
internal sealed class QuotesController(ISender sender) : BaseController(sender)
{
    [HttpPost]
    [ProducesResponseType(typeof(ApiResult<CreatePriceQuoteCommand.Response>), 200)]
    public Task<IActionResult> Create([FromBody] CreatePriceQuoteCommand.Request? model, CancellationToken cancellationToken) =>
        SendAsync(new CreatePriceQuoteCommand(model), cancellationToken);
}