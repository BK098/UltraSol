using FluentValidation;
using UltraSol.Modules.Payment.Application.Contracts;
using UltraSol.Modules.Payment.Domain.Payments;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Payment.Application.Features.Reconciliation;

public sealed record QueryTransactionCommand(Guid OrderId, Guid TransactionId) : ICommand<ApiResult<PaymentDetails>>;

public sealed class QueryTransactionValidator : AbstractValidator<QueryTransactionCommand>
{
    public QueryTransactionValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.TransactionId).NotEmpty();
    }
}

internal sealed class QueryTransactionHandler(PaymentRecovery recovery, PaymentService service) : ICommandHandler<QueryTransactionCommand, ApiResult<PaymentDetails>>
{
    public Task<ApiResult<PaymentDetails>> Handle(QueryTransactionCommand request, CancellationToken ct) =>
        PaymentResults.Run(() => Query(request, ct));
    private async Task<PaymentDetails> Query(QueryTransactionCommand request, CancellationToken ct)
    {
        await recovery.QueryAsync(request.OrderId, request.TransactionId, ct);
        return await service.DetailAsync(request.OrderId, ct);
    }
}

