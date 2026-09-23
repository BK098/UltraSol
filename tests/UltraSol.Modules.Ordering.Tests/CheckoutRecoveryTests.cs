using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Modules.Ordering.Infrastructure.Persistence;
using UltraSol.Modules.Ordering.Infrastructure.Checkout;
using UltraSol.Shared.Application.Responses;
using Xunit;

namespace UltraSol.Modules.Ordering.Tests;

public sealed class CheckoutRecoveryTests(OrderingDatabaseFixture fixture) : IClassFixture<OrderingDatabaseFixture>
{
    [OrderingPostgresFact]
    public async Task Restarted_worker_resumes_each_durable_checkpoint_without_changing_frozen_prices_or_order_id()
    {
        foreach (var checkpoint in new[] { CheckoutStage.Preparing, CheckoutStage.Quoted, CheckoutStage.Reserved })
        {
            var clock = new OrderingTestClock();
            var upstream = new CheckoutDependencies(clock);
            var findCalls = 0;
            upstream.BeforeCatalog = () =>
            {
                if (checkpoint == CheckoutStage.Preparing)
                {
                    throw new InvalidOperationException("Crash after attempt commit.");
                }
            };
            upstream.BeforeReserve = () => checkpoint == CheckoutStage.Quoted
                ? throw new InvalidOperationException("Crash after quote commit.") : Task.CompletedTask;
            upstream.BeforeFind = () =>
            {
                if (++findCalls == 2 && checkpoint == CheckoutStage.Reserved)
                {
                    throw new InvalidOperationException("Crash after reservation reference commit.");
                }
            };
            CartService.CartResponse cart;
            using (var original = CheckoutTestScope.Services(fixture, upstream, clock))
            {
                cart = await CheckoutTestScope.Cart(original, Guid.NewGuid());
                await Assert.ThrowsAsync<InvalidOperationException>(() => Checkout(original, cart, "crash", CheckoutTestScope.Request(cart.ConcurrencyStamp)));
            }
            using var before = fixture.Create();
            var saved = await before.Attempts.AsNoTracking().SingleAsync(value => value.CartId == cart.Id);
            Assert.Equal(checkpoint, saved.Stage);
            upstream.BeforeCatalog = null;
            upstream.BeforeReserve = null;
            upstream.BeforeFind = null;
            upstream.Price = 999;
            clock.Now = clock.Now.AddSeconds(61);
            using var restarted = CheckoutTestScope.Services(fixture, upstream, clock);
            var worker = new CheckoutRecoveryWorker(restarted.GetRequiredService<IServiceScopeFactory>(), clock, NullLogger<CheckoutRecoveryWorker>.Instance);
            await worker.SweepAsync(default);
            await worker.SweepAsync(default);
            var replay = await Checkout(restarted, cart, "crash", CheckoutTestScope.Request(cart.ConcurrencyStamp));
            Assert.Equal(200, replay.StatusCode);
            Assert.Equal(saved.OrderId, replay.Data!.OrderId);
            Assert.Equal(checkpoint == CheckoutStage.Preparing ? 1998 : 240, replay.Data.GrandTotal);
            Assert.Equal(1, upstream.QuoteCalls);
            Assert.Single(upstream.ReservePayloads);
            Assert.Single(upstream.Reservations);
        }
    }

    [OrderingPostgresFact]
    public async Task Compensation_does_not_reopen_cart_on_a_single_missing_reservation_after_local_TTL()
    {
        var clock = new OrderingTestClock();
        var upstream = new CheckoutDependencies(clock) { LoseReserveResponse = true };
        using var services = CheckoutTestScope.Services(fixture, upstream, clock);
        var cart = await CheckoutTestScope.Cart(services, Guid.NewGuid());
        var model = CheckoutTestScope.Request(cart.ConcurrencyStamp);
        var pending = (await Checkout(services, cart, "uncertain", model)).Data!;
        var remote = upstream.Reservations.Values.Single();
        upstream.Reservations.Clear();
        clock.Now = remote.ExpiresAt.AddMinutes(1);
        using (var edit = services.CreateScope())
        {
            var provider = edit.ServiceProvider;
            await provider.GetRequiredService<UltraSol.Modules.Ordering.Domain.Repositories.IOrderingUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var attempt = (await provider.GetRequiredService<IOrderingStore>().AttemptAsync(pending.OperationId, ct))!;
                attempt.Stage = CheckoutStage.Compensating;
                attempt.LeaseUntil = null;
                attempt.ErrorCode = "UncertainReservation";
                attempt.ErrorStatus = 409;
            });
        }
        var retry = await Checkout(services, cart, "uncertain", model);
        Assert.Equal(202, retry.StatusCode);
        using var read = services.CreateScope();
        Assert.Equal("CheckingOut", (await read.ServiceProvider.GetRequiredService<CartService>().GetAsync(cart.Id, cart.GuestAccessToken, default)).Data!.Status);
        upstream.Reservations[remote.OrderId] = remote;
        clock.Now = clock.Now.AddMinutes(2);
        var released = await Checkout(services, cart, "uncertain", model);
        Assert.Equal(409, released.StatusCode);
        Assert.Equal("Expired", upstream.Reservations.Values.Single().Status);
    }

    [OrderingPostgresFact]
    public async Task Definitive_rejection_of_first_reserve_reopens_the_cart()
    {
        var clock = new OrderingTestClock();
        var upstream = new CheckoutDependencies(clock)
        {
            BeforeReserve = () => throw new OrderingFailure(409, "InsufficientStock", "Not enough stock.")
        };
        using var services = CheckoutTestScope.Services(fixture, upstream, clock);
        var cart = await CheckoutTestScope.Cart(services, Guid.NewGuid());
        var rejected = await Checkout(services, cart, "out-of-stock", CheckoutTestScope.Request(cart.ConcurrencyStamp));
        Assert.Equal(409, rejected.StatusCode);
        using var read = services.CreateScope();
        Assert.Equal("Active", (await read.ServiceProvider.GetRequiredService<CartService>().GetAsync(cart.Id, cart.GuestAccessToken, default)).Data!.Status);
        Assert.Empty(upstream.Reservations);
    }

    [OrderingPostgresFact]
    public async Task Lost_reserve_response_recovers_with_frozen_quote_bundle_and_order_id()
    {
        var clock = new OrderingTestClock();
        var bundle = Guid.NewGuid();
        var component = Guid.NewGuid();
        var upstream = new CheckoutDependencies(clock) { LoseReserveResponse = true,
            Items = [new(bundle, Guid.NewGuid(), "BUNDLE", "Original bundle", "", null, true, null, true, [new(component, 3)])] };
        using var services = CheckoutTestScope.Services(fixture, upstream, clock);
        var cart = await CheckoutTestScope.Cart(services, bundle);
        var model = CheckoutTestScope.Request(cart.ConcurrencyStamp);
        var first = await Checkout(services, cart, "lost-response", model);
        Assert.Equal(202, first.StatusCode);
        upstream.Price = 999;
        upstream.Items = [];
        clock.Now = clock.Now.AddMinutes(1);
        var replay = await Checkout(services, cart, "lost-response", model);
        Assert.Equal(200, replay.StatusCode);
        Assert.Equal(240m, replay.Data!.GrandTotal);
        Assert.Equal(1, upstream.QuoteCalls);
        var reserve = Assert.Single(upstream.ReservePayloads);
        Assert.Equal(6, Assert.Single(reserve.Lines).Quantity);
        Assert.Equal(reserve.OrderId, replay.Data.OrderId);
        using var scope = services.CreateScope();
        var order = await scope.ServiceProvider.GetRequiredService<IOrderingStore>().OrderAsync(reserve.OrderId, default);
        Assert.Equal("Original bundle", order!.Lines.Single().ProductName);
        Assert.Equal(component, order.ReservedStock.Single().ProductItemId);
        Assert.Equal(6, order.ReservedStock.Single().Quantity);
        var persisted = await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Attempts.SingleAsync(attempt => attempt.Id == first.Data!.OperationId);
        Assert.Equal(reserve.ExpiresAt, persisted.ExpiresAt);
        Assert.DoesNotContain(cart.GuestAccessToken!, JsonSerializer.Serialize(persisted));
    }

    [OrderingPostgresFact]
    public async Task Concurrent_same_key_replays_one_order_and_other_keys_or_payloads_conflict()
    {
        var clock = new OrderingTestClock();
        var upstream = new CheckoutDependencies(clock);
        using var services = CheckoutTestScope.Services(fixture, upstream, clock);
        var cart = await CheckoutTestScope.Cart(services, Guid.NewGuid());
        var model = CheckoutTestScope.Request(cart.ConcurrencyStamp);
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Checkout(services, cart, "concurrent", model)));
        Assert.All(results, result => Assert.Contains(result.StatusCode, new[] { 200, 201, 202 }));
        var replay = await Checkout(services, cart, "concurrent", model);
        Assert.Equal(200, replay.StatusCode);
        Assert.Single(upstream.Reservations);
        await Assert.ThrowsAsync<OrderingFailure>(() => Checkout(services, cart, "concurrent", model with { Buyer = model.Buyer with { Name = "Other" } }));
        await Assert.ThrowsAsync<OrderingFailure>(() => Checkout(services, cart, "different", model));
        using var db = fixture.Create();
        Assert.Equal(1, await db.Orders.CountAsync(order => order.CartId == cart.Id));
    }

    [OrderingPostgresFact]
    public async Task Expiry_before_order_commit_fails_checkout_and_reopens_cart_without_duplicate_reserve()
    {
        var clock = new OrderingTestClock();
        var upstream = new CheckoutDependencies(clock);
        using var services = CheckoutTestScope.Services(fixture, upstream, clock);
        var cart = await CheckoutTestScope.Cart(services, Guid.NewGuid());
        upstream.AfterReserve = async () =>
        {
            var reservation = upstream.Reservations.Values.Single();
            upstream.Reservations[reservation.OrderId] = reservation with { Status = "Expired" };
            using var scope = services.CreateScope();
            var mailbox = scope.ServiceProvider.GetRequiredService<UltraSol.Shared.Infrastructure.Messaging.Mailbox<OrderingDbContext>>();
            var message = new UltraSol.Shared.IntegrationEvents.Inventory.InventoryReservationExpiredV1(Guid.NewGuid(), reservation.OrderId, 1,
                reservation.ExpiresAt, reservation.Id, reservation.OrderId, reservation.ExpiresAt);
            await mailbox.ReceiveAsync(message.GetType(), JsonSerializer.Serialize(message), default);
        };
        var result = await Checkout(services, cart, "early-expiry", CheckoutTestScope.Request(cart.ConcurrencyStamp));
        Assert.Equal(409, result.StatusCode);
        using var verify = services.CreateScope();
        var restored = (await verify.ServiceProvider.GetRequiredService<CartService>().GetAsync(cart.Id, cart.GuestAccessToken, default)).Data!;
        Assert.Equal("Active", restored.Status);
        using var db = fixture.Create();
        Assert.False(await db.Orders.AnyAsync(order => order.CartId == cart.Id));
    }

    internal static async Task<ApiResult<CheckoutResult>> Checkout(IServiceProvider services, CartService.CartResponse cart, string key, CheckoutCartRequest model)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CheckoutOrchestrator>().CheckoutAsync(cart.Id, key, model, cart.GuestAccessToken, default);
    }
}