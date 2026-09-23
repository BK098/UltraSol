using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Orders.Queries;

public sealed record ListOrdersQuery(PagedFilter Filter) : IQuery<ApiResult<OrderListResponse>>;

public sealed class ListOrdersValidator : AbstractValidator<ListOrdersQuery>
{
    public ListOrdersValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        When(x => x.Filter is not null, () =>
        {
            RuleFor(x => x.Filter.PageIndex).GreaterThan(0);
            RuleFor(x => x.Filter.PageSize).InclusiveBetween(1, 100);
        });
    }
}

internal sealed class ListOrdersHandler(IOrderingReadStore store) : IQueryHandler<ListOrdersQuery, ApiResult<OrderListResponse>>
{
    public Task<ApiResult<OrderListResponse>> Handle(ListOrdersQuery request, CancellationToken ct) =>
        OrderingResults.Run(() => ListAsync(request, ct));

    private async Task<ApiResult<OrderListResponse>> ListAsync(ListOrdersQuery request, CancellationToken ct) =>
        ApiResultBuilder.Success(await store.ListAsync(null, request.Filter, ct));
}