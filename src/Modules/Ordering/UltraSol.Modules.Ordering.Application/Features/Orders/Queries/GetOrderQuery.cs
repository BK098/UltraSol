using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Orders.Queries;

public sealed record GetOrderQuery(Guid OrderId, string? GuestToken) : IQuery<ApiResult<OrderReadService.OrderDetails>>, IAnonymousAuthRequest;

public sealed class GetOrderValidator : AbstractValidator<GetOrderQuery>
{
    public GetOrderValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

internal sealed class GetOrderHandler(OrderReadService service) : IQueryHandler<GetOrderQuery, ApiResult<OrderReadService.OrderDetails>>
{
    public Task<ApiResult<OrderReadService.OrderDetails>> Handle(GetOrderQuery request, CancellationToken ct) =>
        OrderingResults.Run(() => service.GetAsync(request.OrderId, request.GuestToken, false, ct));
}