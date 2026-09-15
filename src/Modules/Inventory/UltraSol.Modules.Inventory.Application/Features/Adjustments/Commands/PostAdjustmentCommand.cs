using FluentValidation;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Inventory.Application.Features.Adjustments.Commands;

public sealed record PostAdjustmentCommand(Guid AdjustmentId, string? ActorId) : ICommand<ApiResult<AdjustmentResult>>;

public sealed class PostAdjustmentCommandValidator : AbstractValidator<PostAdjustmentCommand>
{
    public PostAdjustmentCommandValidator()
    {
        RuleFor(x => x.AdjustmentId).NotEmpty();
    }
}

internal sealed class PostAdjustmentCommandHandler(IInventoryModule module) : ICommandHandler<PostAdjustmentCommand, ApiResult<AdjustmentResult>>
{
    public async Task<ApiResult<AdjustmentResult>> Handle(PostAdjustmentCommand request, CancellationToken ct)
    {
        var result = await module.PostAdjustmentAsync(request.AdjustmentId, request.ActorId, ct);
        return ApiResultBuilder.Success(result);
    }
}
