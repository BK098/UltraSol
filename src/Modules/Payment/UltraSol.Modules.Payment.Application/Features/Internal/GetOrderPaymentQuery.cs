using FluentValidation;
using UltraSol.Modules.Payment.Application.Contracts;
using UltraSol.Modules.Payment.Domain.Payments;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Payment.Application.Features.Internal;

public sealed record GetOrderPaymentQuery(Guid OrderId) : IQuery<ApiResult<PaymentSummary>>;

public sealed class GetOrderPaymentValidator : AbstractValidator<GetOrderPaymentQuery>
{
    public GetOrderPaymentValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

internal sealed class GetOrderPaymentHandler(PaymentService service) : IQueryHandler<GetOrderPaymentQuery, ApiResult<PaymentSummary>>
{
    public Task<ApiResult<PaymentSummary>> Handle(GetOrderPaymentQuery request, CancellationToken ct) =>
        PaymentResults.Run(() => service.GetAsync(request.OrderId, ct));
}

