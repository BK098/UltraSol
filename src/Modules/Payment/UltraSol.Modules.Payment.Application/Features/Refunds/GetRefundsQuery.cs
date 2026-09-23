using FluentValidation;
using UltraSol.Modules.Payment.Application.Contracts;
using UltraSol.Modules.Payment.Domain.Payments;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Payment.Application.Features.Refunds;

public sealed record GetRefundsQuery(int Skip = 0, int Take = 20, string? State = null) : IQuery<ApiResult<PaymentRefund[]>>;

public sealed class GetRefundsValidator : AbstractValidator<GetRefundsQuery>
{
    public GetRefundsValidator()
    {
        RuleFor(x => x.Skip).InclusiveBetween(0, 1000000);
        RuleFor(x => x.Take).InclusiveBetween(1, 100);
        RuleFor(x => x.State).Must(value => value is null or "Requested" or "Approved" or "Processing" or "Refunded");
    }
}

internal sealed class GetRefundsHandler(IPaymentStore store) : IQueryHandler<GetRefundsQuery, ApiResult<PaymentRefund[]>>
{
    public Task<ApiResult<PaymentRefund[]>> Handle(GetRefundsQuery request, CancellationToken ct) =>
        PaymentResults.Run(() => store.ListRefundsAsync(request.Skip, request.Take, request.State, ct));
}

