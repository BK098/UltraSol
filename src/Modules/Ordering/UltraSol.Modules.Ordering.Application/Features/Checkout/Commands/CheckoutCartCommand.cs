using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Checkout.Commands;

public sealed record CheckoutCartCommand(Guid CartId, string IdempotencyKey, CheckoutCartRequest? Model, string? GuestToken) : ICommand<ApiResult<CheckoutResult>>, IAnonymousAuthRequest;

public sealed class CheckoutCartValidator : AbstractValidator<CheckoutCartCommand>
{
    public CheckoutCartValidator()
    {
        RuleFor(x => x.CartId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Model).NotNull();
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.ConcurrencyStamp).NotEmpty();
            RuleFor(x => x.Model!.Buyer).NotNull();
            RuleFor(x => x.Model!.ShippingAddress).NotNull();
            RuleFor(x => x.Model!.PaymentTerm).NotNull();
            When(x => x.Model!.Buyer is not null, () =>
            {
                RuleFor(x => x.Model!.Buyer.Name).NotEmpty();
                RuleFor(x => x.Model!.Buyer.Email).NotEmpty().EmailAddress();
                RuleFor(x => x.Model!.Buyer.Phone).NotEmpty();
            });
        });
    }
}

internal sealed class CheckoutCartHandler(CheckoutOrchestrator service) : ICommandHandler<CheckoutCartCommand, ApiResult<CheckoutResult>>
{
    public Task<ApiResult<CheckoutResult>> Handle(CheckoutCartCommand request, CancellationToken ct) =>
        OrderingResults.Run(() => service.CheckoutAsync(request.CartId, request.IdempotencyKey, request.Model!, request.GuestToken, ct));
}