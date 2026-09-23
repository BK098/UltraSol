using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Ordering.Application.Features.Payments;

public sealed record GetOrderPaymentQuery(Guid OrderId, string? GuestToken) : IQuery<ApiResult<OrderPaymentSummary>>, IAnonymousAuthRequest;

public sealed class GetOrderPaymentValidator : AbstractValidator<GetOrderPaymentQuery>
{
    public GetOrderPaymentValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

internal sealed class GetOrderPaymentHandler(OrderPaymentFacade service) : IQueryHandler<GetOrderPaymentQuery, ApiResult<OrderPaymentSummary>>
{
    public Task<ApiResult<OrderPaymentSummary>> Handle(GetOrderPaymentQuery request, CancellationToken ct) => OrderingResults.Run(async () =>
        ApiResultBuilder.Success(await service.GetAsync(request.OrderId, request.GuestToken, ct)));
}
