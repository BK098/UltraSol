using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Carts.Commands;

public sealed record ChangeCartItemQuantityCommand(Guid CartId, string ConcurrencyStamp, Guid ItemId, int Quantity, string? GuestToken) : ICommand<ApiResult<CartService.CartResponse>>, IAnonymousAuthRequest;

public sealed class ChangeCartItemQuantityValidator : AbstractValidator<ChangeCartItemQuantityCommand>
{
    public ChangeCartItemQuantityValidator()
    {
        RuleFor(x => x.CartId).NotEmpty();
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

internal sealed class ChangeCartItemQuantityHandler(CartService service) : ICommandHandler<ChangeCartItemQuantityCommand, ApiResult<CartService.CartResponse>>
{
    public Task<ApiResult<CartService.CartResponse>> Handle(ChangeCartItemQuantityCommand request, CancellationToken ct) =>
        OrderingResults.Run(() => service.MutateAsync(request.CartId, request.ConcurrencyStamp, request.GuestToken, (cart, now) => cart.ChangeItem(request.ItemId, request.Quantity, now), ct));
}