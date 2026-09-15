using FluentValidation;
using UltraSol.Modules.Inventory.Application.Reads;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Inventory.Application.Features.Stocks.Queries;

public sealed record GetStockQuery(Guid ProductItemId) : IQuery<ApiResult<GetStockQuery.Response>>
{
    public sealed record Response(Guid WarehouseId, Guid ProductItemId, int OnHand, int Reserved, int Available);
}

public sealed class GetStockQueryValidator : AbstractValidator<GetStockQuery>
{
    public GetStockQueryValidator()
    {
        RuleFor(x => x.ProductItemId).NotEmpty();
    }
}

internal sealed class GetStockQueryHandler(IInventoryReadStore reads) : IQueryHandler<GetStockQuery, ApiResult<GetStockQuery.Response>>
{
    public async Task<ApiResult<GetStockQuery.Response>> Handle(GetStockQuery request, CancellationToken ct)
    {
        var result = await reads.StockAsync(request.ProductItemId, ct);
        return ApiResultBuilder.Success(result);
    }
}
