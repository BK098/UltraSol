using FluentValidation;
using UltraSol.Modules.Inventory.Application.Reads;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Inventory.Application.Features.Adjustments.Queries;

public sealed record GetAdjustmentQuery(Guid Id) : IQuery<ApiResult<GetAdjustmentQuery.Response>>
{
    public sealed record Response(Guid Id, string Reason, string? Note, string Status, string ConcurrencyStamp,
        DateTimeOffset CreatedAt, DateTimeOffset? PostedAt, DateTimeOffset? CancelledAt, IReadOnlyList<Line> Lines);
    public sealed record Line(Guid WarehouseId, Guid ProductItemId, int QuantityDelta);
}

public sealed class GetAdjustmentQueryValidator : AbstractValidator<GetAdjustmentQuery>
{
    public GetAdjustmentQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

internal sealed class GetAdjustmentQueryHandler(IInventoryReadStore reads) : IQueryHandler<GetAdjustmentQuery, ApiResult<GetAdjustmentQuery.Response>>
{
    public async Task<ApiResult<GetAdjustmentQuery.Response>> Handle(GetAdjustmentQuery request, CancellationToken ct)
    {
        var result = await reads.AdjustmentAsync(request.Id, ct)
            ?? throw new EntityNotFoundException("InventoryAdjustment", request.Id);
        return ApiResultBuilder.Success(result);
    }
}
