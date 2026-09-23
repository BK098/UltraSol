using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Modules.Ordering.Domain.Orders;
using UltraSol.Modules.Ordering.Domain.Repositories;
using UltraSol.Modules.Ordering.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Messaging;
using UltraSol.Shared.IntegrationEvents.Fulfillment;
using UltraSol.Shared.IntegrationEvents.Inventory;
using Xunit;

namespace UltraSol.Modules.Ordering.Tests;

public sealed class LifecycleTests(OrderingDatabaseFixture fixture) : IClassFixture<OrderingDatabaseFixture>
{
    [OrderingPostgresFact]
    public async Task Crash_after_inventory_release_resumes_the_same_cancellation_once()
    {
        var clock = new OrderingTestClock();
        var upstream = new CheckoutDependencies(clock);
        using var services = CheckoutTestScope.Services(fixture, upstream, clock);
        var cart = await CheckoutTestScope.Cart(services, Guid.NewGuid());
        var checkout = (await CheckoutRecoveryTests.Checkout(services, cart, "release-crash", CheckoutTestScope.Request(cart.ConcurrencyStamp))).Data!;
        upstream.AfterRelease = () => throw new InvalidOperationException("Crash after remote release commit.");
        using (var request = services.CreateScope())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => request.ServiceProvider.GetRequiredService<OrderLifecycle>()
                .CancelAsync(checkout.OrderId!.Value, checkout.ConcurrencyStamp!, "Changed mind", cart.GuestAccessToken, false, default));
        }
        Assert.Equal("Released", upstream.Reservations.Values.Single().Status);
        upstream.AfterRelease = null;
        clock.Now = clock.Now.AddSeconds(61);
        using var recovery = services.CreateScope();
        var store = recovery.ServiceProvider.GetRequiredService<IOrderingStore>();
        var operation = (await store.FindOperationAsync(checkout.OrderId!.Value, "Cancel", default))!;
        var lifecycle = recovery.ServiceProvider.GetRequiredService<OrderLifecycle>();
        await lifecycle.RunAsync(operation.Id, default);
        await lifecycle.RunAsync(operation.Id, default);
        var order = (await store.OrderAsync(checkout.OrderId.Value, default))!;
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal("Changed mind", order.History.Last().Reason);
        Assert.Equal(3, order.History.Count);
    }

    [OrderingPostgresFact]
    public async Task A_different_reservation_persists_a_failed_operation_without_cancelling_the_order()
    {
        var clock = new OrderingTestClock();
        var upstream = new CheckoutDependencies(clock);
        using var services = CheckoutTestScope.Services(fixture, upstream, clock);
        var cart = await CheckoutTestScope.Cart(services, Guid.NewGuid());
        var checkout = (await CheckoutRecoveryTests.Checkout(services, cart, "mismatch", CheckoutTestScope.Request(cart.ConcurrencyStamp))).Data!;
        var reservation = upstream.Reservations.Values.Single();
        upstream.Reservations[reservation.OrderId] = reservation with { Id = Guid.NewGuid() };
        using var scope = services.CreateScope();
        var error = await Assert.ThrowsAsync<OrderingFailure>(() => scope.ServiceProvider.GetRequiredService<OrderLifecycle>()
            .CancelAsync(reservation.OrderId, checkout.ConcurrencyStamp!, "Cancel", cart.GuestAccessToken, false, default));
        Assert.Equal("ReservationMismatch", error.Code);
        var store = scope.ServiceProvider.GetRequiredService<IOrderingStore>();
        Assert.Equal("Failed", (await store.FindOperationAsync(reservation.OrderId, "Cancel", default))!.State);
        Assert.Equal(OrderStatus.Confirmed, (await store.OrderAsync(reservation.OrderId, default))!.Status);
        Assert.Equal("Active", upstream.Reservations.Values.Single().Status);
    }

    [OrderingPostgresFact]
    public async Task Confirmed_expiry_preserves_pending_cancellation_reason_and_inbox_is_idempotent()
    {
        var clock = new OrderingTestClock();
        var upstream = new CheckoutDependencies(clock);
        using var services = CheckoutTestScope.Services(fixture, upstream, clock);
        var cart = await CheckoutTestScope.Cart(services, Guid.NewGuid());
        var checkout = await CheckoutRecoveryTests.Checkout(services, cart, "expiry", CheckoutTestScope.Request(cart.ConcurrencyStamp));
        var reservation = upstream.Reservations.Values.Single();
        using (var intent = services.CreateScope())
        {
            await intent.ServiceProvider.GetRequiredService<IOrderingUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                intent.ServiceProvider.GetRequiredService<IOrderingStore>().Add(new OrderOperation
                {
                    OrderId = checkout.Data!.OrderId!.Value, ReservationId = reservation.Id, Reason = "Customer changed their mind",
                    CreatedAt = clock.Now, UpdatedAt = clock.Now
                });
                return Task.CompletedTask;
            });
        }
        var message = new InventoryReservationExpiredV1(Guid.NewGuid(), reservation.OrderId, 1, reservation.ExpiresAt,
            reservation.Id, reservation.OrderId, reservation.ExpiresAt);
        await Deliver(services, message);
        await Deliver(services, message);
        await Deliver(services, message with { EventId = Guid.NewGuid() });
        using var read = services.CreateScope();
        var store = read.ServiceProvider.GetRequiredService<IOrderingStore>();
        var order = (await store.OrderAsync(reservation.OrderId, default))!;
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal("Customer changed their mind", order.History.Last().Reason);
        Assert.Equal(3, order.History.Count);
        Assert.Equal("Completed", (await store.FindOperationAsync(order.Id, "Cancel", default))!.State);
        using var db = fixture.Create();
        Assert.False(await db.Set<InboxMessage>().AnyAsync(inbox => inbox.Id == Guid.Empty));
        await Assert.ThrowsAnyAsync<Exception>(() => Deliver(services, message with { EventId = Guid.NewGuid(), ContractVersion = 2 }));
    }

    [OrderingPostgresFact]
    public async Task Consumed_stock_prevents_cancel_and_fulfillment_completes_once_without_payment_or_issue_calls()
    {
        var clock = new OrderingTestClock();
        var upstream = new CheckoutDependencies(clock);
        using var services = CheckoutTestScope.Services(fixture, upstream, clock);
        var cart = await CheckoutTestScope.Cart(services, Guid.NewGuid());
        var checkout = (await CheckoutRecoveryTests.Checkout(services, cart, "complete", CheckoutTestScope.Request(cart.ConcurrencyStamp))).Data!;
        var reservation = upstream.Reservations.Values.Single();
        upstream.Reservations[reservation.OrderId] = reservation with { Status = "Consumed" };
        using (var cancel = services.CreateScope())
        {
            var error = await Assert.ThrowsAsync<OrderingFailure>(() => cancel.ServiceProvider.GetRequiredService<OrderLifecycle>()
                .CancelAsync(reservation.OrderId, checkout.ConcurrencyStamp!, "Cancel", cart.GuestAccessToken, false, default));
            Assert.Equal("ReservationConsumed", error.Code);
        }
        var message = new FulfillmentCompletedV1(Guid.NewGuid(), reservation.OrderId, 1, clock.Now,
            reservation.OrderId, reservation.Id, Guid.NewGuid(), clock.Now);
        await Deliver(services, message);
        await Deliver(services, message);
        Guid operation;
        using (var worker = services.CreateScope())
        {
            var store = worker.ServiceProvider.GetRequiredService<IOrderingStore>();
            operation = (await store.FindOperationAsync(reservation.OrderId, "Complete", default))!.Id;
            await worker.ServiceProvider.GetRequiredService<OrderLifecycle>().RunAsync(operation, default);
        }
        using var read = services.CreateScope();
        await read.ServiceProvider.GetRequiredService<OrderLifecycle>().RunAsync(operation, default);
        var order = (await read.ServiceProvider.GetRequiredService<IOrderingStore>().OrderAsync(reservation.OrderId, default))!;
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(3, order.History.Count);
        Assert.Equal("COD", order.PaymentTerm.Type);
        await Assert.ThrowsAnyAsync<Exception>(() => Deliver(services, message with { EventId = Guid.NewGuid(), ReservationId = Guid.NewGuid() }));
    }

    [OrderingPostgresFact]
    public async Task Cancel_is_replayable_and_rejects_a_different_reason()
    {
        var clock = new OrderingTestClock();
        var upstream = new CheckoutDependencies(clock);
        using var services = CheckoutTestScope.Services(fixture, upstream, clock);
        var cart = await CheckoutTestScope.Cart(services, Guid.NewGuid());
        var checkout = (await CheckoutRecoveryTests.Checkout(services, cart, "cancel", CheckoutTestScope.Request(cart.ConcurrencyStamp))).Data!;
        async Task<OrderOperationResult> Cancel(string reason)
        {
            using var scope = services.CreateScope();
            return (await scope.ServiceProvider.GetRequiredService<OrderLifecycle>().CancelAsync(checkout.OrderId!.Value,
                checkout.ConcurrencyStamp!, reason, cart.GuestAccessToken, false, default)).Data!;
        }
        var first = await Cancel("Changed mind");
        var replay = await Cancel("Changed mind");
        Assert.Equal(first, replay);
        Assert.Equal("Completed", first.State);
        await Assert.ThrowsAsync<OrderingFailure>(() => Cancel("Different reason"));
        Assert.Equal("Released", upstream.Reservations.Values.Single().Status);
    }

    internal static async Task Deliver<T>(IServiceProvider services, T message)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<Mailbox<OrderingDbContext>>().ReceiveAsync(typeof(T), JsonSerializer.Serialize(message), default);
    }
}