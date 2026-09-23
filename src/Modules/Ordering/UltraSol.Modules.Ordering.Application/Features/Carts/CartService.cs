using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Domain.Carts;
using UltraSol.Modules.Ordering.Domain.Repositories;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Ordering.Application.Features.Carts;

public sealed class CartService(IOrderingStore store, IOrderingUnitOfWork unit, OrderingAccess access, IPricingQuoteClient prices, TimeProvider clock)
{
    public sealed record CartItemResponse(Guid Id, Guid ProductItemId, int Quantity);
    public sealed record CartResponse(Guid Id, string Currency, string Status, string ConcurrencyStamp, CartItemResponse[] Items, string? GuestAccessToken = null);
    public static CartResponse Response(ShoppingCart cart, string? token = null) => new(cart.Id, cart.Currency, cart.Status.ToString(), cart.ConcurrencyStamp,
        cart.Items.Select(item => new CartItemResponse(item.Id, item.ProductItemId, item.Quantity)).ToArray(), token);

    public Task<ApiResult<CartResponse>> CreateAsync(string currency, CancellationToken ct) => unit.ExecuteInTransactionAsync(token =>
    {
        var secret = access.UserId is null ? OrderingAccess.NewToken() : null;
        var hash = secret is null ? null : OrderingAccess.Hash(secret);
        var cart = ShoppingCart.Create(hash is null ? access.UserKey : "g:" + hash, access.UserId, hash, currency, clock.GetUtcNow());
        store.Add(cart);
        return Task.FromResult(ApiResultBuilder.Success(Response(cart, secret), statusCode: 201));
    }, ct);

    public async Task<ShoppingCart> OwnedAsync(Guid id, string? secret, CancellationToken ct)
    {
        var cart = await store.CartAsync(id, ct) ?? throw new OrderingFailure(404, "NotFound", "Cart was not found.");
        access.Demand(cart.OwnerKey, cart.GuestTokenHash, secret);
        return cart;
    }

    public async Task<ApiResult<CartResponse>> GetAsync(Guid id, string? secret, CancellationToken ct) => ApiResultBuilder.Success(Response(await OwnedAsync(id, secret, ct)));

    public Task<ApiResult<CartResponse>> MutateAsync(Guid id, string stamp, string? secret, Action<ShoppingCart, DateTimeOffset> action, CancellationToken ct)
    {
        return unit.ExecuteInTransactionAsync(async token =>
        {
            var cart = await OwnedAsync(id, secret, token);
            if (cart.ConcurrencyStamp != stamp)
            {
                throw new OrderingFailure(409, "CartConcurrencyConflict", "Cart changed. Reload and retry.");
            }
            action(cart, clock.GetUtcNow());
            return ApiResultBuilder.Success(Response(cart));
        }, ct);
    }

    public async Task<ApiResult<PriceQuote>> QuoteAsync(Guid id, string? secret, CancellationToken ct)
    {
        var cart = await OwnedAsync(id, secret, ct);
        if (cart.Status != CartStatus.Active || cart.Items.Count == 0)
        {
            throw new OrderingFailure(409, "CartNotActive", "An active nonempty cart is required.");
        }
        var lines = cart.Items.Select(item => new RequestedLine(item.ProductItemId, item.Quantity)).ToArray();
        var request = new QuoteRequest("Retail", cart.Currency, null, null, null, lines);
        var quote = await prices.QuoteAsync(request, ct);
        CheckoutRules.ValidateQuote(request, quote);
        return ApiResultBuilder.Success(quote);
    }
}