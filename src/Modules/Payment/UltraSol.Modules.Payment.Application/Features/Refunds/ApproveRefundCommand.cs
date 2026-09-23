using FluentValidation;
using UltraSol.Modules.Payment.Application.Contracts;
using UltraSol.Modules.Payment.Domain.Payments;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Payment.Application.Features.Refunds;

public sealed record ApproveRefundCommand(Guid OrderId, Guid RefundId, string ConcurrencyStamp) : ICommand<ApiResult<PaymentRefund>>;

public sealed class ApproveRefundValidator : AbstractValidator<ApproveRefundCommand>
{
    public ApproveRefundValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.RefundId).NotEmpty();
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
    }
}

internal sealed class ApproveRefundHandler(PaymentService service) : ICommandHandler<ApproveRefundCommand, ApiResult<PaymentRefund>>
{
    public Task<ApiResult<PaymentRefund>> Handle(ApproveRefundCommand request, CancellationToken ct) =>
        PaymentResults.Run(() => service.ApproveAsync(request.OrderId, request.RefundId, request.ConcurrencyStamp, ct));
}

