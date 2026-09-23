using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Carts.Commands;

public sealed record ClearCartCommand(Guid CartId, string ConcurrencyStamp, string? GuestToken) : ICommand<ApiResult<CartService.CartResponse>>, IAnonymousAuthRequest;

public sealed class ClearCartValidator : AbstractValidator<ClearCartCommand>
{
    public ClearCartValidator()
    {
        RuleFor(x => x.CartId).NotEmpty();
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
    }
}

internal sealed class ClearCartHandler(CartService service) : ICommandHandler<ClearCartCommand, ApiResult<CartService.CartResponse>>
{
    public Task<ApiResult<CartService.CartResponse>> Handle(ClearCartCommand request, CancellationToken ct) =>
        OrderingResults.Run(() => service.MutateAsync(request.CartId, request.ConcurrencyStamp, request.GuestToken, (cart, now) => cart.Clear(now), ct));
}