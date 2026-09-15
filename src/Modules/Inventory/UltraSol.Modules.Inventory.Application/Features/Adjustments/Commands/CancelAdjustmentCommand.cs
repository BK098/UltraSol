using FluentValidation;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Inventory.Application.Features.Adjustments.Commands;

public sealed record CancelAdjustmentCommand(Guid AdjustmentId, string? ActorId) : ICommand<ApiResult<AdjustmentResult>>;

public sealed class CancelAdjustmentCommandValidator : AbstractValidator<CancelAdjustmentCommand>
{
    public CancelAdjustmentCommandValidator()
    {
        RuleFor(x => x.AdjustmentId).NotEmpty();
    }
}

internal sealed class CancelAdjustmentCommandHandler(IInventoryModule module) : ICommandHandler<CancelAdjustmentCommand, ApiResult<AdjustmentResult>>
{
    public async Task<ApiResult<AdjustmentResult>> Handle(CancelAdjustmentCommand request, CancellationToken ct)
    {
        var result = await module.CancelAdjustmentAsync(request.AdjustmentId, request.ActorId, ct);
        return ApiResultBuilder.Success(result);
    }
}
