using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Ordering.Application.Features.Payments;

public sealed record CreateOrderVnpaySessionCommand(Guid OrderId, string? GuestToken, string Key, string IpAddress)
    : ICommand<ApiResult<OrderPaymentSession>>, IAnonymousAuthRequest;

public sealed class CreateOrderVnpaySessionValidator : AbstractValidator<CreateOrderVnpaySessionCommand>
{
    public CreateOrderVnpaySessionValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.IpAddress).Must(value => System.Net.IPAddress.TryParse(value, out _));
    }
}

internal sealed class CreateOrderVnpaySessionHandler(OrderPaymentFacade service) : ICommandHandler<CreateOrderVnpaySessionCommand, ApiResult<OrderPaymentSession>>
{
    public Task<ApiResult<OrderPaymentSession>> Handle(CreateOrderVnpaySessionCommand request, CancellationToken ct) => OrderingResults.Run(async () =>
        ApiResultBuilder.Success(await service.CreateAsync(request.OrderId, request.GuestToken, request.Key, request.IpAddress, ct)));
}
