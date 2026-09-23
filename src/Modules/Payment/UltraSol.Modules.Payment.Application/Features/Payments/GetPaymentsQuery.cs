using FluentValidation;
using UltraSol.Modules.Payment.Application.Contracts;
using UltraSol.Modules.Payment.Domain.Payments;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Payment.Application.Features.Payments;

public sealed record GetPaymentsQuery(int Skip = 0, int Take = 20, bool Overdue = false) : IQuery<ApiResult<PaymentSummary[]>>;

public sealed class GetPaymentsValidator : AbstractValidator<GetPaymentsQuery>
{
    public GetPaymentsValidator()
    {
        RuleFor(x => x.Skip).InclusiveBetween(0, 1000000);
        RuleFor(x => x.Take).InclusiveBetween(1, 100);
    }
}

internal sealed class GetPaymentsHandler(PaymentService service) : IQueryHandler<GetPaymentsQuery, ApiResult<PaymentSummary[]>>
{
    public Task<ApiResult<PaymentSummary[]>> Handle(GetPaymentsQuery request, CancellationToken ct) =>
        PaymentResults.Run(() => service.ListAsync(request.Skip, request.Take, request.Overdue, ct));
}

