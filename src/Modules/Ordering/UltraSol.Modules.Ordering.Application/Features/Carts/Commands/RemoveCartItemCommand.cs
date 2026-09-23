using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Carts.Commands;

public sealed record RemoveCartItemCommand(Guid CartId, string ConcurrencyStamp, Guid ItemId, string? GuestToken) : ICommand<ApiResult<CartService.CartResponse>>, IAnonymousAuthRequest;

public sealed class RemoveCartItemValidator : AbstractValidator<RemoveCartItemCommand>
{
    public RemoveCartItemValidator()
    {
        RuleFor(x => x.CartId).NotEmpty();
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
    }
}

internal sealed class RemoveCartItemHandler(CartService service) : ICommandHandler<RemoveCartItemCommand, ApiResult<CartService.CartResponse>>
{
    public Task<ApiResult<CartService.CartResponse>> Handle(RemoveCartItemCommand request, CancellationToken ct) =>
        OrderingResults.Run(() => service.MutateAsync(request.CartId, request.ConcurrencyStamp, request.GuestToken, (cart, now) => cart.RemoveItem(request.ItemId, now), ct));
}