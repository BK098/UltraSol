using FluentValidation;
using UltraSol.Modules.Inventory.Application.Reads;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Inventory.Application.Features.Reservations.Queries;

public sealed record GetReservationQuery(Guid Id) : IQuery<ApiResult<GetReservationQuery.Response>>
{
    public sealed record Response(Guid Id, Guid OrderId, string Status, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt,
        DateTimeOffset? ReleasedAt, DateTimeOffset? ConsumedAt, DateTimeOffset? ExpiredAt, IReadOnlyList<Line> Lines);
    public sealed record Line(Guid WarehouseId, Guid ProductItemId, int Quantity);
}

public sealed class GetReservationQueryValidator : AbstractValidator<GetReservationQuery>
{
    public GetReservationQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

internal sealed class GetReservationQueryHandler(IInventoryReadStore reads) : IQueryHandler<GetReservationQuery, ApiResult<GetReservationQuery.Response>>
{
    public async Task<ApiResult<GetReservationQuery.Response>> Handle(GetReservationQuery request, CancellationToken ct)
    {
        var result = await reads.ReservationAsync(request.Id, ct)
            ?? throw new EntityNotFoundException("StockReservation", request.Id);
        return ApiResultBuilder.Success(result);
    }
}
