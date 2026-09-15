using FluentValidation;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Inventory.Application.Features.Reservations.Commands;

public sealed record ReleaseReservationCommand(Guid ReservationId, string? ActorId) : ICommand<ApiResult<ReservationResult>>;

public sealed class ReleaseReservationCommandValidator : AbstractValidator<ReleaseReservationCommand>
{
    public ReleaseReservationCommandValidator()
    {
        RuleFor(x => x.ReservationId).NotEmpty();
    }
}

internal sealed class ReleaseReservationCommandHandler(IInventoryModule module) : ICommandHandler<ReleaseReservationCommand, ApiResult<ReservationResult>>
{
    public async Task<ApiResult<ReservationResult>> Handle(ReleaseReservationCommand request, CancellationToken ct)
    {
        var result = await module.ReleaseAsync(request.ReservationId, request.ActorId, ct);
        return ApiResultBuilder.Success(result);
    }
}
