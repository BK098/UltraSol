using FluentValidation;
using UltraSol.Modules.Payment.Application.Contracts;
using UltraSol.Modules.Payment.Domain.Payments;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Payment.Application.Features.Receipts;

public sealed record RecordReceiptCommand(Guid OrderId, string Key, ManualReceipt Receipt) : ICommand<ApiResult<PaymentTransaction>>;

public sealed class RecordReceiptValidator : AbstractValidator<RecordReceiptCommand>
{
    public RecordReceiptValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Receipt).NotNull();
    }
}

internal sealed class RecordReceiptHandler(PaymentService service) : ICommandHandler<RecordReceiptCommand, ApiResult<PaymentTransaction>>
{
    public Task<ApiResult<PaymentTransaction>> Handle(RecordReceiptCommand request, CancellationToken ct) =>
        PaymentResults.Run(() => service.RecordAsync(request.OrderId, request.Key, request.Receipt, ct));
}

