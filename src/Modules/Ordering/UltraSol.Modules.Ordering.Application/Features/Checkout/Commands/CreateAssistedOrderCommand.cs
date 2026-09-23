using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Checkout.Commands;

public sealed record CreateAssistedOrderCommand(string IdempotencyKey, AssistedOrderRequest? Model) : ICommand<ApiResult<CheckoutResult>>;

public sealed class CreateAssistedOrderValidator : AbstractValidator<CreateAssistedOrderCommand>
{
    public CreateAssistedOrderValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Model).NotNull();
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.Buyer).NotNull();
            RuleFor(x => x.Model!.ShippingAddress).NotNull();
            RuleFor(x => x.Model!.PaymentTerm).NotNull();
            RuleFor(x => x.Model!.Lines).NotNull();
            RuleForEach(x => x.Model!.Lines).NotNull();
        });
    }
}

internal sealed class CreateAssistedOrderHandler(CheckoutOrchestrator service) : ICommandHandler<CreateAssistedOrderCommand, ApiResult<CheckoutResult>>
{
    public Task<ApiResult<CheckoutResult>> Handle(CreateAssistedOrderCommand request, CancellationToken ct) =>
        OrderingResults.Run(() => service.AssistedAsync(request.IdempotencyKey, request.Model!, ct));
}