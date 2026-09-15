using FluentValidation;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Modules.Inventory.Application.Validation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Inventory.Application.Features.Adjustments.Commands;

public sealed record CreateAdjustmentCommand(CreateAdjustmentRequest? Model, string? ActorId) : ICommand<ApiResult<AdjustmentResult>>;

public sealed class CreateAdjustmentCommandValidator : AbstractValidator<CreateAdjustmentCommand>
{
    public CreateAdjustmentCommandValidator()
    {
        RuleFor(x => x.Model).NotNull();
        RuleFor(x => x.Model!).SetValidator(new CreateAdjustmentRequestValidator()).When(x => x.Model is not null);
    }
}

internal sealed class CreateAdjustmentCommandHandler(IInventoryModule module) : ICommandHandler<CreateAdjustmentCommand, ApiResult<AdjustmentResult>>
{
    public async Task<ApiResult<AdjustmentResult>> Handle(CreateAdjustmentCommand request, CancellationToken ct)
    {
        var result = await module.CreateAdjustmentAsync(request.Model!, request.ActorId, ct);
        return ApiResultBuilder.Success(result, statusCode: 201);
    }
}
