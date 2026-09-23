using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Orders.Commands;

public sealed record AdminCancelOrderCommand(Guid OrderId, string ConcurrencyStamp, string Reason) : ICommand<ApiResult<OrderOperationResult>>;

public sealed class AdminCancelOrderValidator : AbstractValidator<AdminCancelOrderCommand>
{
    public AdminCancelOrderValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
    }
}

internal sealed class AdminCancelOrderHandler(OrderLifecycle service) : ICommandHandler<AdminCancelOrderCommand, ApiResult<OrderOperationResult>>
{
    public Task<ApiResult<OrderOperationResult>> Handle(AdminCancelOrderCommand request, CancellationToken ct) =>
        OrderingResults.Run(() => service.CancelAsync(request.OrderId, request.ConcurrencyStamp, request.Reason, null, true, ct));
}