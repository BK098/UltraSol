using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Carts.Queries;

public sealed record GetCartQuery(Guid CartId, string? GuestToken) : IQuery<ApiResult<CartService.CartResponse>>, IAnonymousAuthRequest;

public sealed class GetCartValidator : AbstractValidator<GetCartQuery>
{
    public GetCartValidator()
    {
        RuleFor(x => x.CartId).NotEmpty();
    }
}

internal sealed class GetCartHandler(CartService service) : IQueryHandler<GetCartQuery, ApiResult<CartService.CartResponse>>
{
    public Task<ApiResult<CartService.CartResponse>> Handle(GetCartQuery request, CancellationToken ct) =>
        OrderingResults.Run(() => service.GetAsync(request.CartId, request.GuestToken, ct));
}