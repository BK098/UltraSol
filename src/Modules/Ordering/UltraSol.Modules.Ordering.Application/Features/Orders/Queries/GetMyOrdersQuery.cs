using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Orders.Queries;

public sealed record GetMyOrdersQuery(PagedFilter Filter) : IQuery<ApiResult<OrderListResponse>>, ISelfServiceAuthRequest;

public sealed class GetMyOrdersValidator : AbstractValidator<GetMyOrdersQuery>
{
    public GetMyOrdersValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        When(x => x.Filter is not null, () =>
        {
            RuleFor(x => x.Filter.PageIndex).GreaterThan(0);
            RuleFor(x => x.Filter.PageSize).InclusiveBetween(1, 100);
        });
    }
}

internal sealed class GetMyOrdersHandler(IOrderingReadStore store, OrderingAccess access) : IQueryHandler<GetMyOrdersQuery, ApiResult<OrderListResponse>>
{
    public Task<ApiResult<OrderListResponse>> Handle(GetMyOrdersQuery request, CancellationToken ct) =>
        OrderingResults.Run(() => ListAsync(request, ct));

    private async Task<ApiResult<OrderListResponse>> ListAsync(GetMyOrdersQuery request, CancellationToken ct) =>
        ApiResultBuilder.Success(await store.ListAsync(access.UserKey, request.Filter, ct));
}