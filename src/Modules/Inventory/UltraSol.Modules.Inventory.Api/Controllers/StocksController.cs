using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Modules.Inventory.Application.Features.Stocks.Commands;
using UltraSol.Modules.Inventory.Application.Features.Stocks.Queries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Inventory.Api.Controllers;

[Route("api/inventory/stocks")]
internal sealed class StocksController(ISender sender) : BaseController(sender)
{
    [HttpPost("/api/inventory/receipts")]
    [ProducesResponseType(typeof(ApiResult<ReceiptResult>), 201)]
    public Task<IActionResult> Receive([FromBody] ReceiveStockRequest? model, CancellationToken ct) =>
        SendAsync(new ReceiveStockCommand(model, ActorId()), ct);

    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<PaginatedResult<GetStocksQuery.Response>>), 200)]
    public Task<IActionResult> GetStocks([FromQuery] PagedFilter? filter, CancellationToken ct) =>
        SendAsync(new GetStocksQuery(filter), ct);

    [HttpGet("{productItemId:guid}")]
    [ProducesResponseType(typeof(ApiResult<GetStockQuery.Response>), 200)]
    public Task<IActionResult> GetStock(Guid productItemId, CancellationToken ct) =>
        SendAsync(new GetStockQuery(productItemId), ct);

    [HttpGet("availability")]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<GetAvailabilityQuery.Response>>), 200)]
    public Task<IActionResult> GetAvailability([FromQuery] Guid[]? productItemIds, CancellationToken ct) =>
        SendAsync(new GetAvailabilityQuery(productItemIds), ct);

    private string? ActorId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
}
