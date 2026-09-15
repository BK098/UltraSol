using FluentValidation;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Modules.Inventory.Application.Validation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Inventory.Application.Features.Reservations.Commands;

public sealed record ReserveStockCommand(ReserveStockRequest? Model, string? ActorId) : ICommand<ApiResult<ReservationResult>>;

public sealed class ReserveStockCommandValidator : AbstractValidator<ReserveStockCommand>
{
    public ReserveStockCommandValidator()
    {
        RuleFor(x => x.Model).NotNull();
        RuleFor(x => x.Model!).SetValidator(new ReserveStockRequestValidator()).When(x => x.Model is not null);
    }
}

internal sealed class ReserveStockCommandHandler(IInventoryModule module) : ICommandHandler<ReserveStockCommand, ApiResult<ReservationResult>>
{
    public async Task<ApiResult<ReservationResult>> Handle(ReserveStockCommand request, CancellationToken ct)
    {
        var result = await module.ReserveAsync(request.Model!, request.ActorId, ct);
        return ApiResultBuilder.Success(result, statusCode: 201);
    }
}
