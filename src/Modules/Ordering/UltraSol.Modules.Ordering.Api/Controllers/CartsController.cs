using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts.Commands;
using UltraSol.Modules.Ordering.Application.Features.Carts.Queries;
using UltraSol.Modules.Ordering.Application.Features.Checkout.Commands;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Ordering.Api.Controllers;

[Route("api/ordering/carts")]
internal sealed class CartsController(ISender sender) : BaseController(sender)
{
    public sealed record CreateCartRequest(string Currency);
    public sealed record AddItemRequest(string ConcurrencyStamp, Guid ProductItemId, int Quantity);
    public sealed record ChangeQuantityRequest(string ConcurrencyStamp, int Quantity);
    public sealed record VersionRequest(string ConcurrencyStamp);

    [HttpPost]
    public Task<IActionResult> Create(CreateCartRequest model, CancellationToken ct) => SendAsync(new CreateCartCommand(model.Currency), ct);

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => SendAsync(new GetCartQuery(id, GuestToken()), ct);

    [HttpPost("{id:guid}/items")]
    public Task<IActionResult> AddItem(Guid id, AddItemRequest model, CancellationToken ct) =>
        SendAsync(new AddCartItemCommand(id, model.ConcurrencyStamp, model.ProductItemId, model.Quantity, GuestToken()), ct);

    [HttpPut("{id:guid}/items/{itemId:guid}")]
    public Task<IActionResult> ChangeQuantity(Guid id, Guid itemId, ChangeQuantityRequest model, CancellationToken ct) =>
        SendAsync(new ChangeCartItemQuantityCommand(id, model.ConcurrencyStamp, itemId, model.Quantity, GuestToken()), ct);

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public Task<IActionResult> RemoveItem(Guid id, Guid itemId, [FromBody] VersionRequest model, CancellationToken ct) =>
        SendAsync(new RemoveCartItemCommand(id, model.ConcurrencyStamp, itemId, GuestToken()), ct);

    [HttpDelete("{id:guid}/items")]
    public Task<IActionResult> Clear(Guid id, [FromBody] VersionRequest model, CancellationToken ct) =>
        SendAsync(new ClearCartCommand(id, model.ConcurrencyStamp, GuestToken()), ct);

    [HttpPost("{id:guid}/quote")]
    public Task<IActionResult> Quote(Guid id, CancellationToken ct) => SendAsync(new QuoteCartQuery(id, GuestToken()), ct);

    [HttpPost("{id:guid}/checkout")]
    public Task<IActionResult> Checkout(Guid id, CheckoutCartRequest? model, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        SendAsync(new CheckoutCartCommand(id, key, model, GuestToken()), ct);

    private string? GuestToken() => Request.Headers["X-Ordering-Guest-Token"].FirstOrDefault();
}