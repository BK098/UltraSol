using FluentValidation;
using UltraSol.Modules.Inventory.Application.Reads;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Inventory.Application.Features.Stocks.Queries;

public sealed record GetStocksQuery(PagedFilter? Filter) : IQuery<ApiResult<PaginatedResult<GetStocksQuery.Response>>>
{
    public sealed record Response(Guid WarehouseId, Guid ProductItemId, int OnHand, int Reserved, int Available);
}

public sealed class GetStocksQueryValidator : AbstractValidator<GetStocksQuery>
{
    public GetStocksQueryValidator()
    {
        InventoryQueryPaging.AddRules(this, query => query.Filter);
    }
}

internal sealed class GetStocksQueryHandler(IInventoryReadStore reads) : IQueryHandler<GetStocksQuery, ApiResult<PaginatedResult<GetStocksQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetStocksQuery.Response>>> Handle(GetStocksQuery request, CancellationToken ct)
    {
        var result = await reads.StocksAsync(InventoryQueryPaging.Create(request.Filter!), ct);
        return ApiResultBuilder.Success(result);
    }
}
