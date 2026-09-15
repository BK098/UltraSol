using FluentValidation;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Inventory.Application.Features.Reservations.Commands;

public sealed record IssueReservationCommand(Guid ReservationId, string? ActorId) : ICommand<ApiResult<ReservationResult>>;

public sealed class IssueReservationCommandValidator : AbstractValidator<IssueReservationCommand>
{
    public IssueReservationCommandValidator()
    {
        RuleFor(x => x.ReservationId).NotEmpty();
    }
}

internal sealed class IssueReservationCommandHandler(IInventoryModule module) : ICommandHandler<IssueReservationCommand, ApiResult<ReservationResult>>
{
    public async Task<ApiResult<ReservationResult>> Handle(IssueReservationCommand request, CancellationToken ct)
    {
        var result = await module.IssueAsync(request.ReservationId, request.ActorId, ct);
        return ApiResultBuilder.Success(result);
    }
}
