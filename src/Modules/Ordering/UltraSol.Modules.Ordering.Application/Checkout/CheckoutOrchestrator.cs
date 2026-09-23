using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Domain.Carts;
using UltraSol.Modules.Ordering.Domain.Orders;
using UltraSol.Modules.Ordering.Domain.Repositories;
using UltraSol.Modules.Ordering.Domain.ValueObjects;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Ordering.Application.Checkout;

public sealed class CheckoutOrchestrator(IOrderingStore store, IOrderingUnitOfWork unit, CartService carts, OrderingAccess access,
    ICatalogCheckoutClient catalog, IPricingQuoteClient pricing, IInventoryReservationClient inventory, OrderingOptions options, TimeProvider clock,
    UltraSol.Modules.Ordering.Application.Messaging.IOrderingOutbox outbox)
{
    public async Task<ApiResult<CheckoutResult>> CheckoutAsync(Guid cartId, string key, CheckoutCartRequest model, string? secret, CancellationToken ct)
    {
        key = OrderingRule.Text(key, 128, "Idempotency-Key");
        ValidateContact(model);
        model = model with { Buyer = new(model.Buyer.Name.Trim(), model.Buyer.Email.Trim(), model.Buyer.Phone.Trim()) };
        var fingerprint = CheckoutRules.Fingerprint(new { cartId, model });
        store.Reset();
        var started = await unit.ExecuteInTransactionAsync(async token =>
        {
            await store.LockAsync("cart:" + cartId, token);
            var cart = await carts.OwnedAsync(cartId, secret, token);
            var replay = await store.FindAttemptAsync(cart.OwnerKey, key, token);
            if (replay is not null)
            {
                DemandSame(replay, fingerprint);
                return (replay.Id, false);
            }
            if (cart.ConcurrencyStamp != model.ConcurrencyStamp)
            {
                throw new OrderingFailure(409, "CartConcurrencyConflict", "Cart changed. Reload and retry.");
            }
            var input = new CheckoutInput(cart.Id, cart.Currency, OrderType.Retail,
                new BuyerSnapshot(cart.IdentityUserId is null ? "Guest" : "Registered", cart.IdentityUserId, null, null,
                    model.Buyer.Name.Trim(), model.Buyer.Email.Trim(), model.Buyer.Phone.Trim()), model.ShippingAddress,
                model.BillingAddress, model.PaymentTerm, CheckoutRules.NormalizeLines(cart.Items.Select(item => new RequestedLine(item.ProductItemId, item.Quantity)).ToArray()),
                null, null, cart.ConcurrencyStamp);
            cart.BeginCheckout(clock.GetUtcNow());
            var attempt = NewAttempt(cart.OwnerKey, cart.IdentityUserId, cart.GuestTokenHash, key, fingerprint, input);
            store.Add(attempt);
            return (attempt.Id, true);
        }, ct);
        await RunAsync(started.Id, ct);
        return await ResultAsync(started.Id, secret, false, started.Item2, ct);
    }

    public async Task<ApiResult<CheckoutResult>> AssistedAsync(string key, AssistedOrderRequest model, CancellationToken ct)
    {
        key = OrderingRule.Text(key, 128, "Idempotency-Key");
        model.Buyer.Validate();
        model.ShippingAddress.Validate();
        model.BillingAddress?.Validate();
        model.PaymentTerm.Validate();
        OrderingRule.Require(Enum.IsDefined(model.OrderType), "Unknown order type.");
        OrderingRule.Require(model.OrderType == OrderType.Contract ? model.ContractId is { } id && id != Guid.Empty : model.ContractId is null, "Invalid contract context.");
        var input = new CheckoutInput(null, OrderingRule.Currency(model.Currency), model.OrderType, model.Buyer, model.ShippingAddress,
            model.BillingAddress, model.PaymentTerm, CheckoutRules.NormalizeLines(model.Lines), model.ContractId, model.NegotiationTransactionRef, null);
        OrderingRule.Require(input.Lines.All(line => line.NegotiatedPriceId is null) || input.NegotiationTransactionRef is { } reference && reference != Guid.Empty, "Negotiated prices need a transaction reference.");
        var fingerprint = CheckoutRules.Fingerprint(input);
        var owner = access.UserKey;
        store.Reset();
        var started = await unit.ExecuteInTransactionAsync(async token =>
        {
            await store.LockAsync("checkout:" + owner + ":" + key, token);
            var replay = await store.FindAttemptAsync(owner, key, token);
            if (replay is not null)
            {
                DemandSame(replay, fingerprint);
                return (replay.Id, false);
            }
            var attempt = NewAttempt(owner, access.UserId, null, key, fingerprint, input);
            store.Add(attempt);
            return (attempt.Id, true);
        }, ct);
        await RunAsync(started.Id, ct);
        return await ResultAsync(started.Id, null, false, started.Item2, ct);
    }

    public async Task<ApiResult<CheckoutResult>> ResultAsync(Guid id, string? secret, bool trusted, bool created, CancellationToken ct)
    {
        store.Reset();
        var attempt = await store.AttemptAsync(id, ct) ?? throw new OrderingFailure(404, "NotFound", "Checkout was not found.");
        if (!trusted)
        {
            access.Demand(attempt.OwnerKey, attempt.GuestTokenHash, secret);
        }
        if (attempt.Stage == CheckoutStage.Failed)
        {
            return ApiResultBuilder.Error<CheckoutResult>(attempt.ErrorMessage ?? "Checkout failed.", attempt.ErrorStatus ?? 409,
                new Dictionary<string, string[]> { ["Code"] = [attempt.ErrorCode ?? "CheckoutFailed"] });
        }
        if (attempt.Stage == CheckoutStage.Completed)
        {
            var order = await store.OrderAsync(attempt.OrderId, ct) ?? throw new InvalidOperationException("Completed checkout has no order.");
            return ApiResultBuilder.Success(Result(attempt, order), statusCode: created ? 201 : 200);
        }
        if (!attempt.ReserveStarted && attempt.ErrorStatus >= 500)
        {
            return ApiResultBuilder.Error<CheckoutResult>(attempt.ErrorMessage ?? "Checkout dependency is unavailable.", 503,
                new Dictionary<string, string[]> { ["Code"] = [attempt.ErrorCode ?? "DependencyUnavailable"] });
        }
        return ApiResultBuilder.Success(new CheckoutResult(attempt.Id, attempt.Stage.ToString(), null, null, null, null, null, null,
            attempt.ExpiresAt, attempt.ErrorCode), statusCode: 202);
    }

    public async Task RunAsync(Guid id, CancellationToken ct)
    {
        var lease = Guid.NewGuid();
        var attempt = await InTransaction(async token =>
        {
            await store.LockAsync("attempt:" + id, token);
            var value = await store.AttemptAsync(id, token);
            if (value is null || value.Stage is CheckoutStage.Completed or CheckoutStage.Failed || value.LeaseUntil > clock.GetUtcNow())
            {
                return null;
            }
            value.LeaseToken = lease;
            value.LeaseUntil = clock.GetUtcNow().AddSeconds(60);
            return value;
        }, ct);
        if (attempt is null)
        {
            return;
        }
        var reserveRejected = false;
        try
        {
            if (attempt.Stage == CheckoutStage.Preparing)
            {
                var items = await catalog.GetAsync(attempt.Input.Lines.Select(line => line.ProductItemId).ToArray(), ct);
                var physical = CheckoutRules.Expand(attempt.Input.Lines, items);
                var input = attempt.Input;
                var quoteRequest = new QuoteRequest(input.OrderType.ToString(), input.Currency, input.Buyer.CustomerId,
                    input.ContractId, input.NegotiationTransactionRef, input.Lines);
                var quote = await pricing.QuoteAsync(quoteRequest, ct);
                CheckoutRules.ValidateQuote(quoteRequest, quote);
                attempt = await Update(id, lease, (value, _) =>
                {
                    value.CatalogItems = items;
                    value.StockLines = physical;
                    value.Quote = quote;
                    value.ExpiresAt = CheckoutRules.Normalize(clock.GetUtcNow().AddMinutes(options.ReservationMinutes));
                    value.Stage = CheckoutStage.Quoted;
                    return Task.CompletedTask;
                }, ct);
            }
            if (attempt.Stage == CheckoutStage.Quoted)
            {
                var previouslyStarted = attempt.ReserveStarted;
                attempt = await Update(id, lease, (value, _) =>
                {
                    value.ReserveStarted = true;
                    return Task.CompletedTask;
                }, ct);
                var reservation = await inventory.FindAsync(attempt.OrderId, ct);
                if (reservation is null)
                {
                    try
                    {
                        reservation = await inventory.ReserveAsync(new ReserveRequest(attempt.OrderId, attempt.ExpiresAt!.Value, attempt.StockLines!), ct);
                    }
                    catch (OrderingFailure error) when (!previouslyStarted && error.Status is >= 400 and < 500)
                    {
                        // A definitive rejection of the first reserve proves there is no earlier in-flight request.
                        reserveRejected = true;
                        throw;
                    }
                }
                CheckoutRules.ValidateReservation(attempt, reservation);
                if (reservation.Status != "Active")
                {
                    throw new OrderingFailure(409, "Reservation" + reservation.Status, "Reservation is no longer active.");
                }
                attempt = await Update(id, lease, (value, _) =>
                {
                    value.ReservationId = reservation.Id;
                    value.Stage = CheckoutStage.Reserved;
                    return Task.CompletedTask;
                }, ct);
            }
            if (attempt.Stage == CheckoutStage.Reserved)
            {
                var reservation = await inventory.FindAsync(attempt.OrderId, ct);
                if (reservation is null || reservation.Status != "Active")
                {
                    throw new OrderingFailure(409, "ReservationUnavailable", "Reservation is no longer active.");
                }
                CheckoutRules.ValidateReservation(attempt, reservation);
                await Update(id, lease, async (value, token) =>
                {
                    if (value.ExpiryObserved || value.ExpiresAt <= clock.GetUtcNow())
                    {
                        throw new OrderingFailure(409, "ReservationExpired", "Reservation expired before order creation.");
                    }
                    var input = value.Input;
                    var lines = input.Lines.Select(line =>
                    {
                        var product = value.CatalogItems!.Single(item => item.ProductItemId == line.ProductItemId);
                        var price = value.Quote!.Lines.Single(item => item.ProductItemId == line.ProductItemId);
                        return new OrderLineSnapshot(product.ProductItemId, product.ProductId, product.ProductName, product.SkuCode,
                            product.VariantDescription, product.ImageUrl, line.Quantity, price.Snapshot(input.Currency));
                    }).ToArray();
                    var owner = input.Buyer.IdentityUserId is { } buyerId ? "u:" + buyerId.ToString("N") : value.OwnerKey;
                    var order = Order.Place(value.OrderId, await store.NextOrderNumberAsync(token), value.CartId, owner,
                        input.Buyer.IdentityUserId, value.GuestTokenHash, input.OrderType, value.CartId is null ? "Assisted" : "Storefront", input.Buyer,
                        input.ShippingAddress, input.BillingAddress, input.PaymentTerm, value.Quote!.QuoteId, value.ReservationId!.Value,
                        value.ExpiresAt!.Value, input.Currency, lines, clock.GetUtcNow(), input.ContractId,
                        value.StockLines!.Select(line => new ReservedStockLine(line.ProductItemId, line.Quantity)).ToArray());
                    order.Confirm(clock.GetUtcNow());
                    store.Add(order);
                    outbox.Placed(order);
                    if (value.CartId is { } cartId)
                    {
                        var cart = await store.CartAsync(cartId, token) ?? throw new InvalidOperationException("Checkout cart missing.");
                        cart.EndCheckout(true, clock.GetUtcNow());
                    }
                    value.Stage = CheckoutStage.Completed;
                    value.Result = Result(value, order);
                    value.LeaseToken = null;
                    value.LeaseUntil = null;
                }, ct);
            }
            if (attempt.Stage == CheckoutStage.Compensating)
            {
                await Compensate(id, lease, attempt, ct);
            }
        }
        catch (OrderingFailure error) when (error.Code == "LeaseLost")
        {
            // Another runner or the expiry inbox owns the durable result; the caller reads it again.
        }
        catch (OrderingFailure error)
        {
            await Failure(id, lease, error, ct, reserveRejected);
        }
        catch (DomainException error)
        {
            await Failure(id, lease, new OrderingFailure(409, error.Code ?? "InvalidOrder", error.Message), ct);
        }
        catch (OverflowException)
        {
            await Failure(id, lease, new OrderingFailure(400, "AmountOverflow", "Quantity or amount is too large."), ct);
        }
    }

    private async Task Failure(Guid id, Guid lease, OrderingFailure error, CancellationToken ct, bool reserveRejected = false)
    {
        var attempt = await InTransaction(async token =>
        {
            await store.LockAsync("attempt:" + id, token);
            var value = await store.AttemptAsync(id, token);
            if (value is null || value.LeaseToken != lease || value.Stage is CheckoutStage.Completed or CheckoutStage.Failed)
            {
                return null;
            }
            value.ErrorCode = error.Code;
            value.ErrorMessage = error.Message;
            value.ErrorStatus = error.Status;
            value.UpdatedAt = clock.GetUtcNow();
            if (error.Status >= 500)
            {
                value.LeaseUntil = clock.GetUtcNow().AddSeconds(5);
                return null;
            }
            value.Stage = value.ReserveStarted && !reserveRejected ? CheckoutStage.Compensating : CheckoutStage.Failed;
            if (value.Stage == CheckoutStage.Failed)
            {
                await Reopen(value, token);
                value.LeaseUntil = null;
                value.LeaseToken = null;
            }
            return value;
        }, ct);
        if (attempt?.Stage == CheckoutStage.Compensating)
        {
            await Compensate(id, lease, attempt, ct);
        }
    }

    private async Task Compensate(Guid id, Guid lease, CheckoutAttempt attempt, CancellationToken ct)
    {
        try
        {
            var reservation = await inventory.FindAsync(attempt.OrderId, ct);
            if (reservation is null)
            {
                // A timed-out reserve can still commit after this read, even after our local clock passes TTL.
                return;
            }
            else
            {
                var released = await inventory.ReleaseAsync(attempt.OrderId, ct);
                if (released.Id != reservation.Id || released.OrderId != attempt.OrderId || released.Status is not ("Released" or "Expired"))
                {
                    return;
                }
            }
            await Update(id, lease, async (value, token) =>
            {
                value.Stage = CheckoutStage.Failed;
                value.LeaseToken = null;
                value.LeaseUntil = null;
                await Reopen(value, token);
            }, ct);
        }
        catch (OrderingFailure)
        {
            // Durable compensation remains pending; the recovery worker retries with a new scope.
        }
    }

    private async Task Reopen(CheckoutAttempt value, CancellationToken ct)
    {
        if (value.CartId is { } id)
        {
            var cart = await store.CartAsync(id, ct);
            if (cart?.Status == CartStatus.CheckingOut)
            {
                cart.EndCheckout(false, clock.GetUtcNow());
            }
        }
    }

    private Task<CheckoutAttempt> Update(Guid id, Guid lease, Func<CheckoutAttempt, CancellationToken, Task> change, CancellationToken ct)
    {
        return InTransaction(async token =>
        {
            await store.LockAsync("attempt:" + id, token);
            var value = await store.AttemptAsync(id, token) ?? throw new OrderingFailure(409, "LeaseLost", "Checkout no longer exists.");
            if (value.LeaseToken != lease || value.Stage is CheckoutStage.Completed or CheckoutStage.Failed)
            {
                throw new OrderingFailure(409, "LeaseLost", "Another worker updated checkout.");
            }
            value.LeaseUntil = clock.GetUtcNow().AddSeconds(60);
            value.UpdatedAt = clock.GetUtcNow();
            await change(value, token);
            return value;
        }, ct);
    }

    private Task<T> InTransaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        store.Reset();
        return unit.ExecuteInTransactionAsync(action, ct);
    }

    private CheckoutAttempt NewAttempt(string owner, Guid? user, string? hash, string key, string fingerprint, CheckoutInput input) => new()
    {
        OwnerKey = owner, IdentityUserId = user, GuestTokenHash = hash, IdempotencyKey = key, Fingerprint = fingerprint, Input = input,
        CartId = input.CartId, NegotiationTransactionRef = input.NegotiationTransactionRef, CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow()
    };

    private static void DemandSame(CheckoutAttempt attempt, string fingerprint)
    {
        if (attempt.Fingerprint != fingerprint)
        {
            throw new OrderingFailure(409, "IdempotencyConflict", "Idempotency key belongs to a different request.");
        }
    }

    private static void ValidateContact(CheckoutCartRequest model)
    {
        new BuyerSnapshot("Guest", null, null, null, model.Buyer.Name.Trim(), model.Buyer.Email.Trim(), model.Buyer.Phone.Trim()).Validate();
        model.ShippingAddress.Validate();
        model.BillingAddress?.Validate();
        model.PaymentTerm.Validate();
        OrderingRule.Require(model.PaymentTerm.Type != "Net", "Net payment requires an assisted order.");
    }

    private static CheckoutResult Result(CheckoutAttempt attempt, Order order) => new(attempt.Id, CheckoutStage.Completed.ToString(), order.Id,
        order.OrderNumber, order.GrandTotal, order.Currency, order.Status.ToString(), order.ConcurrencyStamp, order.ReservationExpiresAt);
}
