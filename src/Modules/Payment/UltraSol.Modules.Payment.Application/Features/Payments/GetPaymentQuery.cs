using FluentValidation;
using UltraSol.Modules.Payment.Application.Contracts;
using UltraSol.Modules.Payment.Domain.Payments;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Payment.Application.Features.Payments;

public sealed record GetPaymentQuery(Guid OrderId) : IQuery<ApiResult<PaymentDetails>>;

public sealed class GetPaymentValidator : AbstractValidator<GetPaymentQuery>
{
    public GetPaymentValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

internal sealed class GetPaymentHandler(PaymentService service) : IQueryHandler<GetPaymentQuery, ApiResult<PaymentDetails>>
{
    public Task<ApiResult<PaymentDetails>> Handle(GetPaymentQuery request, CancellationToken ct) =>
        PaymentResults.Run(() => service.DetailAsync(request.OrderId, ct));
}

