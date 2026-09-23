using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Ordering.Application.Features.Payments;

public sealed record OrderPaymentContext(Guid OrderId, long OrderVersion, string OrderStatus, string OrderNumber, decimal Amount,
    string Currency, string PaymentTerm, int? NetDays, DateTimeOffset ReservationExpiresAt, DateTimeOffset? CompletedAt);
public sealed record GetPaymentContextQuery(Guid OrderId) : IQuery<ApiResult<OrderPaymentContext>>;

public sealed class GetPaymentContextValidator : AbstractValidator<GetPaymentContextQuery>
{
    public GetPaymentContextValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

internal sealed class GetPaymentContextHandler(IOrderingStore store) : IQueryHandler<GetPaymentContextQuery, ApiResult<OrderPaymentContext>>
{
    public Task<ApiResult<OrderPaymentContext>> Handle(GetPaymentContextQuery request, CancellationToken ct) => OrderingResults.Run(async () =>
    {
        var order = await store.OrderAsync(request.OrderId, ct) ?? throw new OrderingFailure(404, "NotFound", "Order was not found.");
        return ApiResultBuilder.Success(new OrderPaymentContext(order.Id, order.OrderVersion, order.Status.ToString(), order.OrderNumber,
            order.GrandTotal, order.Currency, order.PaymentTerm.Type, order.PaymentTerm.NetDays, order.ReservationExpiresAt, order.CompletedAt));
    });
}
