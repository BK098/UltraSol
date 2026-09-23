using FluentValidation;
using UltraSol.Modules.Payment.Application.Contracts;
using UltraSol.Modules.Payment.Domain.Payments;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Payment.Application.Features.Internal;

public sealed record CreateVnpaySessionCommand(Guid OrderId, string Key, string IpAddress) : ICommand<ApiResult<PaymentSession>>;

public sealed class CreateVnpaySessionValidator : AbstractValidator<CreateVnpaySessionCommand>
{
    public CreateVnpaySessionValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.IpAddress).Must(value => System.Net.IPAddress.TryParse(value, out _));
    }
}

internal sealed class CreateVnpaySessionHandler(PaymentService service) : ICommandHandler<CreateVnpaySessionCommand, ApiResult<PaymentSession>>
{
    public Task<ApiResult<PaymentSession>> Handle(CreateVnpaySessionCommand request, CancellationToken ct) =>
        PaymentResults.Run(() => service.CreateSessionAsync(request.OrderId, request.Key, request.IpAddress, ct));
}

