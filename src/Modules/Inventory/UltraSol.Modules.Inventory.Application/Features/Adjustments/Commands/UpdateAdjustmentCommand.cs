using FluentValidation;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Modules.Inventory.Application.Validation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Inventory.Application.Features.Adjustments.Commands;

public sealed record UpdateAdjustmentCommand(Guid AdjustmentId, UpdateAdjustmentRequest? Model, string? ActorId) : ICommand<ApiResult<AdjustmentResult>>;

public sealed class UpdateAdjustmentCommandValidator : AbstractValidator<UpdateAdjustmentCommand>
{
    public UpdateAdjustmentCommandValidator()
    {
        RuleFor(x => x.AdjustmentId).NotEmpty();
        RuleFor(x => x.Model).NotNull();
        RuleFor(x => x.Model!).SetValidator(new UpdateAdjustmentRequestValidator()).When(x => x.Model is not null);
    }
}

internal sealed class UpdateAdjustmentCommandHandler(IInventoryModule module) : ICommandHandler<UpdateAdjustmentCommand, ApiResult<AdjustmentResult>>
{
    public async Task<ApiResult<AdjustmentResult>> Handle(UpdateAdjustmentCommand request, CancellationToken ct)
    {
        var result = await module.UpdateAdjustmentAsync(request.AdjustmentId, request.Model!, request.ActorId, ct);
        return ApiResultBuilder.Success(result);
    }
}
