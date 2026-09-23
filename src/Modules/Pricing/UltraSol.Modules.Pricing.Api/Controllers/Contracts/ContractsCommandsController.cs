using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Pricing.Application.Features.Contracts.Commands;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Pricing.Api.Controllers.Contracts;

[Authorize]
[Route("api/pricing/contracts")]
internal sealed class ContractsCommandsController(ISender sender) : BaseController(sender)
{
    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateContractDto? model, CancellationToken cancellationToken) =>
        SendAsync(new CreateContractCommand(model), cancellationToken);

    [HttpPost("{id:guid}/activate")]
    public Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken) =>
        SendAsync(new ActivateContractCommand(id), cancellationToken);

    [HttpPost("{id:guid}/terminate")]
    public Task<IActionResult> Terminate(Guid id, CancellationToken cancellationToken) =>
        SendAsync(new TerminateContractCommand(id), cancellationToken);
}