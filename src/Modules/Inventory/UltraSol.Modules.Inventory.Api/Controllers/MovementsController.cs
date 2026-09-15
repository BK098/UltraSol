using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Inventory.Application.Features.Movements.Queries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Inventory.Api.Controllers;

[Route("api/inventory/movements")]
internal sealed class MovementsController(ISender sender) : BaseController(sender)
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<PaginatedResult<GetMovementsQuery.Response>>), 200)]
    public Task<IActionResult> GetMovements([FromQuery] PagedFilter? filter, [FromQuery] Guid? productItemId,
        [FromQuery] string? movementType, [FromQuery] Guid? referenceId, [FromQuery] string? referenceType,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct) =>
        SendAsync(new GetMovementsQuery(filter, productItemId, movementType, referenceId, referenceType, from, to), ct);
}
