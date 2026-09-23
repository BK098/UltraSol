using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Orders.Commands;

public sealed record CancelOrderCommand(Guid OrderId, string ConcurrencyStamp, string Reason, string? GuestToken) : ICommand<ApiResult<OrderOperationResult>>, IAnonymousAuthRequest;

public sealed class CancelOrderValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
    }
}

internal sealed class CancelOrderHandler(OrderLifecycle service) : ICommandHandler<CancelOrderCommand, ApiResult<OrderOperationResult>>
{
    public Task<ApiResult<OrderOperationResult>> Handle(CancelOrderCommand request, CancellationToken ct) =>
        OrderingResults.Run(() => service.CancelAsync(request.OrderId, request.ConcurrencyStamp, request.Reason, request.GuestToken, false, ct));
}