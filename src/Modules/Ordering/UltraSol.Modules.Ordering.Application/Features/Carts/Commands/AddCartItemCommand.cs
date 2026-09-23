using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Carts.Commands;

public sealed record AddCartItemCommand(Guid CartId, string ConcurrencyStamp, Guid ProductItemId, int Quantity, string? GuestToken) : ICommand<ApiResult<CartService.CartResponse>>, IAnonymousAuthRequest;

public sealed class AddCartItemValidator : AbstractValidator<AddCartItemCommand>
{
    public AddCartItemValidator()
    {
        RuleFor(x => x.CartId).NotEmpty();
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
        RuleFor(x => x.ProductItemId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

internal sealed class AddCartItemHandler(CartService service) : ICommandHandler<AddCartItemCommand, ApiResult<CartService.CartResponse>>
{
    public Task<ApiResult<CartService.CartResponse>> Handle(AddCartItemCommand request, CancellationToken ct) =>
        OrderingResults.Run(() => service.MutateAsync(request.CartId, request.ConcurrencyStamp, request.GuestToken, (cart, now) => cart.AddItem(request.ProductItemId, request.Quantity, now), ct));
}