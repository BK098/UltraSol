using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Orders.Queries;

public sealed record GetAdminOrderQuery(Guid OrderId) : IQuery<ApiResult<OrderReadService.OrderDetails>>;

public sealed class GetAdminOrderValidator : AbstractValidator<GetAdminOrderQuery>
{
    public GetAdminOrderValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

internal sealed class GetAdminOrderHandler(OrderReadService service) : IQueryHandler<GetAdminOrderQuery, ApiResult<OrderReadService.OrderDetails>>
{
    public Task<ApiResult<OrderReadService.OrderDetails>> Handle(GetAdminOrderQuery request, CancellationToken ct) =>
        OrderingResults.Run(() => service.GetAsync(request.OrderId, null, true, ct));
}