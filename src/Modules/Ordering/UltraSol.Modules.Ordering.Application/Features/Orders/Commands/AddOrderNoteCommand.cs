using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Orders.Commands;

public sealed record AddOrderNoteCommand(Guid OrderId, string ConcurrencyStamp, string Text) : ICommand<ApiResult<Guid>>;

public sealed class AddOrderNoteValidator : AbstractValidator<AddOrderNoteCommand>
{
    public AddOrderNoteValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
    }
}

internal sealed class AddOrderNoteHandler(OrderReadService service) : ICommandHandler<AddOrderNoteCommand, ApiResult<Guid>>
{
    public Task<ApiResult<Guid>> Handle(AddOrderNoteCommand request, CancellationToken ct) =>
        OrderingResults.Run(() => service.AddNoteAsync(request.OrderId, request.ConcurrencyStamp, request.Text, ct));
}