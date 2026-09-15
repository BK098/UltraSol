using FluentValidation;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Inventory.Application.Features.Reservations.Commands;

public sealed record ReleaseReservationByOrderCommand(Guid OrderId, string? ActorId) : ICommand<ApiResult<ReservationResult>>;

public sealed class ReleaseReservationByOrderCommandValidator : AbstractValidator<ReleaseReservationByOrderCommand>
{
    public ReleaseReservationByOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

internal sealed class ReleaseReservationByOrderCommandHandler(IInventoryModule module) : ICommandHandler<ReleaseReservationByOrderCommand, ApiResult<ReservationResult>>
{
    public async Task<ApiResult<ReservationResult>> Handle(ReleaseReservationByOrderCommand request, CancellationToken ct)
    {
        var result = await module.ReleaseByOrderAsync(request.OrderId, request.ActorId, ct);
        return ApiResultBuilder.Success(result);
    }
}
