using FluentValidation;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Inventory.Application.Features.Stocks.Queries;

public sealed record GetAvailabilityQuery(IReadOnlyList<Guid>? ProductItemIds) : IQuery<ApiResult<IReadOnlyList<GetAvailabilityQuery.Response>>>
{
    public sealed record Response(Guid WarehouseId, Guid ProductItemId, int OnHand, int Reserved, int Available);
}

public sealed class GetAvailabilityQueryValidator : AbstractValidator<GetAvailabilityQuery>
{
    public GetAvailabilityQueryValidator()
    {
        RuleFor(x => x.ProductItemIds).Custom((ids, context) =>
        {
            if (ids is null || ids.Count is < 1 or > 200)
            {
                context.AddFailure("ProductItemIds must contain between 1 and 200 items.");
                return;
            }
            if (ids.Any(id => id == Guid.Empty))
            {
                context.AddFailure("ProductItemIds must not contain empty values.");
            }
            if (ids.Distinct().Count() != ids.Count)
            {
                context.AddFailure("Duplicate ProductItemIds are not allowed.");
            }
        });
    }
}

internal sealed class GetAvailabilityQueryHandler(IInventoryModule module)
    : IQueryHandler<GetAvailabilityQuery, ApiResult<IReadOnlyList<GetAvailabilityQuery.Response>>>
{
    public async Task<ApiResult<IReadOnlyList<GetAvailabilityQuery.Response>>> Handle(GetAvailabilityQuery request, CancellationToken ct)
    {
        var result = await module.GetAvailabilityAsync(request.ProductItemIds!, ct);
        IReadOnlyList<GetAvailabilityQuery.Response> response = result
            .Select(row => new GetAvailabilityQuery.Response(row.WarehouseId, row.ProductItemId, row.OnHand, row.Reserved, row.Available))
            .ToArray();
        return ApiResultBuilder.Success(response);
    }
}
