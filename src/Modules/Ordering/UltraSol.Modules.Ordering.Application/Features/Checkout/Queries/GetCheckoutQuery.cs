using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Checkout.Queries;

public sealed record GetCheckoutQuery(Guid OperationId, string? GuestToken) : IQuery<ApiResult<CheckoutResult>>, IAnonymousAuthRequest;

public sealed class GetCheckoutValidator : AbstractValidator<GetCheckoutQuery>
{
    public GetCheckoutValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
    }
}

internal sealed class GetCheckoutHandler(CheckoutOrchestrator service) : IQueryHandler<GetCheckoutQuery, ApiResult<CheckoutResult>>
{
    public Task<ApiResult<CheckoutResult>> Handle(GetCheckoutQuery request, CancellationToken ct) =>
        OrderingResults.Run(() => service.ResultAsync(request.OperationId, request.GuestToken, false, false, ct));
}