using FluentValidation;
using UltraSol.Modules.Payment.Application.Contracts;
using UltraSol.Modules.Payment.Domain.Payments;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Payment.Application.Features.Reconciliation;

public sealed record ReconcileTransactionCommand(Guid OrderId, Guid TransactionId, string ConcurrencyStamp, bool Received, string EvidenceReference, DateTimeOffset ReceivedAt, string? ProviderTransactionNo) : ICommand<ApiResult<PaymentTransaction>>;

public sealed class ReconcileTransactionValidator : AbstractValidator<ReconcileTransactionCommand>
{
    public ReconcileTransactionValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.TransactionId).NotEmpty();
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
        RuleFor(x => x.EvidenceReference).NotEmpty().MaximumLength(500);
    }
}

internal sealed class ReconcileTransactionHandler(PaymentService service) : ICommandHandler<ReconcileTransactionCommand, ApiResult<PaymentTransaction>>
{
    public Task<ApiResult<PaymentTransaction>> Handle(ReconcileTransactionCommand request, CancellationToken ct) =>
        PaymentResults.Run(() => service.ReconcileAsync(request.OrderId, request.TransactionId, request.ConcurrencyStamp, request.Received, request.EvidenceReference, request.ReceivedAt, request.ProviderTransactionNo, ct));
}

