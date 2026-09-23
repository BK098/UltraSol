using FluentValidation;
using UltraSol.Modules.Payment.Application.Contracts;
using UltraSol.Modules.Payment.Domain.Payments;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Payment.Application.Features.Refunds;

public sealed record ConfirmRefundCommand(Guid OrderId, Guid RefundId, string ConcurrencyStamp, string EvidenceReference) : ICommand<ApiResult<PaymentRefund>>;

public sealed class ConfirmRefundValidator : AbstractValidator<ConfirmRefundCommand>
{
    public ConfirmRefundValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.RefundId).NotEmpty();
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
        RuleFor(x => x.EvidenceReference).NotEmpty().MaximumLength(500);
    }
}

internal sealed class ConfirmRefundHandler(PaymentService service) : ICommandHandler<ConfirmRefundCommand, ApiResult<PaymentRefund>>
{
    public Task<ApiResult<PaymentRefund>> Handle(ConfirmRefundCommand request, CancellationToken ct) =>
        PaymentResults.Run(() => service.ConfirmRefundAsync(request.OrderId, request.RefundId, request.ConcurrencyStamp, request.EvidenceReference, ct));
}

