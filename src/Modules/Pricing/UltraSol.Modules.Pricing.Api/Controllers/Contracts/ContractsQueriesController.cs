using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Pricing.Application.Features.Contracts.Queries;
using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Pricing.Api.Controllers.Contracts;

[Authorize]
[Route("api/pricing/contracts")]
internal sealed class ContractsQueriesController(ISender sender) : BaseController(sender)
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<PaginatedResult<GetContractsQuery.Response>>), 200)]
    public Task<IActionResult> GetContracts([FromQuery] PagedFilter filter, [FromQuery] Guid? customerId, [FromQuery] ContractStatus? status, CancellationToken ct) =>
        SendAsync(new GetContractsQuery(filter, customerId, status), ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResult<GetContractDetailQuery.Response>), 200)]
    public Task<IActionResult> GetContractDetail(Guid id, CancellationToken ct) =>
        SendAsync(new GetContractDetailQuery(id), ct);
}