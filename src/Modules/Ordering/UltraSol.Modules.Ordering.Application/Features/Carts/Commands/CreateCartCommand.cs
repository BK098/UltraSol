using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Carts.Commands;

public sealed record CreateCartCommand(string Currency) : ICommand<ApiResult<CartService.CartResponse>>, IAnonymousAuthRequest;

public sealed class CreateCartValidator : AbstractValidator<CreateCartCommand>
{
    public CreateCartValidator()
    {
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}

internal sealed class CreateCartHandler(CartService service) : ICommandHandler<CreateCartCommand, ApiResult<CartService.CartResponse>>
{
    public Task<ApiResult<CartService.CartResponse>> Handle(CreateCartCommand request, CancellationToken ct) =>
        OrderingResults.Run(() => service.CreateAsync(request.Currency, ct));
}