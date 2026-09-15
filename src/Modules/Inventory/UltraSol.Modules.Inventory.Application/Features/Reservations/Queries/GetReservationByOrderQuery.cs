using FluentValidation;
using UltraSol.Modules.Inventory.Application.Reads;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Inventory.Application.Features.Reservations.Queries;

public sealed record GetReservationByOrderQuery(Guid OrderId) : IQuery<ApiResult<GetReservationQuery.Response>>;

public sealed class GetReservationByOrderQueryValidator : AbstractValidator<GetReservationByOrderQuery>
{
    public GetReservationByOrderQueryValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

internal sealed class GetReservationByOrderQueryHandler(IInventoryReadStore reads)
    : IQueryHandler<GetReservationByOrderQuery, ApiResult<GetReservationQuery.Response>>
{
    public async Task<ApiResult<GetReservationQuery.Response>> Handle(GetReservationByOrderQuery request, CancellationToken ct)
    {
        var result = await reads.ReservationByOrderAsync(request.OrderId, ct)
            ?? throw new EntityNotFoundException("StockReservation", request.OrderId);
        return ApiResultBuilder.Success(result);
    }
}
