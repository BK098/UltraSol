using FluentValidation;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Carts.Queries;

public sealed record QuoteCartQuery(Guid CartId, string? GuestToken) : IQuery<ApiResult<PriceQuote>>, IAnonymousAuthRequest;

public sealed class QuoteCartValidator : AbstractValidator<QuoteCartQuery>
{
    public QuoteCartValidator()
    {
        RuleFor(x => x.CartId).NotEmpty();
    }
}

internal sealed class QuoteCartHandler(CartService service) : IQueryHandler<QuoteCartQuery, ApiResult<PriceQuote>>
{
    public Task<ApiResult<PriceQuote>> Handle(QuoteCartQuery request, CancellationToken ct) =>
        OrderingResults.Run(() => service.QuoteAsync(request.CartId, request.GuestToken, ct));
}