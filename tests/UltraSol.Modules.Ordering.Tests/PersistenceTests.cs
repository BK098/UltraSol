using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Domain.Carts;
using UltraSol.Modules.Ordering.Domain.Orders;
using UltraSol.Modules.Ordering.Domain.ValueObjects;
using UltraSol.Modules.Ordering.Infrastructure.Checkout;
using UltraSol.Modules.Ordering.Infrastructure.Persistence;
using UltraSol.Modules.Ordering.Infrastructure.Repositories;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Repositories;
using Xunit;

namespace UltraSol.Modules.Ordering.Tests;

public sealed class PersistenceTests(OrderingDatabaseFixture fixture) : IClassFixture<OrderingDatabaseFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);

    [OrderingPostgresFact]
    public async Task AggregateAndProcessSnapshotsRoundTripWithoutLosingChildren()
    {
        var orderId = Guid.NewGuid();
        var attempt = Attempt("owner", "round-trip", orderId);
        await using (var db = fixture.Create())
        {
            var store = new OrderingStore(db);
            await Unit(db).ExecuteInTransactionAsync(async ct =>
            {
                store.Add(Order.Place(orderId, await store.NextOrderNumberAsync(ct), null, "owner", null, "hash", OrderType.Retail,
                    "Storefront", attempt.Input.Buyer, attempt.Input.ShippingAddress, attempt.Input.BillingAddress,
                    attempt.Input.PaymentTerm, attempt.Quote!.QuoteId, attempt.ReservationId!.Value, attempt.ExpiresAt!.Value,
                    "VND", [Line()], Now));
                store.Add(attempt);
            });
        }

        await using var read = fixture.Create();
        var loadedOrder = await new OrderingStore(read).OrderAsync(orderId, default);
        var loadedAttempt = await read.Attempts.AsNoTracking().SingleAsync(value => value.Id == attempt.Id);

        Assert.NotNull(loadedOrder);
        Assert.Single(loadedOrder.Lines);
        Assert.Single(loadedOrder.History);
        Assert.Equal(Json(attempt.Input), Json(loadedAttempt.Input));
        Assert.Equal(Json(attempt.CatalogItems), Json(loadedAttempt.CatalogItems));
        Assert.Equal(Json(attempt.Quote), Json(loadedAttempt.Quote));
        Assert.Equal(Json(attempt.StockLines), Json(loadedAttempt.StockLines));
        Assert.Equal(Json(attempt.Result), Json(loadedAttempt.Result));
    }

    [OrderingPostgresFact]
    public async Task DirtyCartItemRefreshesParentStamp()
    {
        var cart = ShoppingCart.Create("owner", null, "hash", "VND", Now);
        cart.AddItem(Guid.NewGuid(), 1, Now);
        await using (var db = fixture.Create())
        {
            await Unit(db).ExecuteInTransactionAsync(_ =>
            {
                new OrderingStore(db).Add(cart);
                return Task.CompletedTask;
            });
        }

        await using var update = fixture.Create();
        var store = new OrderingStore(update);
        var original = "";
        await Unit(update).ExecuteInTransactionAsync(async ct =>
        {
            var loaded = await store.CartAsync(cart.Id, ct) ?? throw new InvalidOperationException();
            original = loaded.ConcurrencyStamp;
            update.Entry(loaded.Items.Single()).Property(nameof(CartItem.Quantity)).CurrentValue = 2;
        });
        await using var verify = fixture.Create();
        var persisted = await new OrderingStore(verify).CartAsync(cart.Id, default);
        Assert.Equal(2, persisted!.Items.Single().Quantity);
        Assert.NotEqual(original, persisted.ConcurrencyStamp);
    }

    [OrderingPostgresFact]
    public async Task RemovingAndClearingCartItemsDeleteOnlyTheOrphanedRows()
    {
        var cart = ShoppingCart.Create("cart-orphans", null, "hash", "VND", Now);
        cart.AddItem(Guid.NewGuid(), 1, Now);
        cart.AddItem(Guid.NewGuid(), 2, Now);
        await using (var db = fixture.Create())
        {
            await Unit(db).ExecuteInTransactionAsync(_ =>
            {
                new OrderingStore(db).Add(cart);
                return Task.CompletedTask;
            });
        }

        await using (var db = fixture.Create())
        {
            var store = new OrderingStore(db);
            await Unit(db).ExecuteInTransactionAsync(async ct =>
            {
                var loaded = await store.CartAsync(cart.Id, ct) ?? throw new InvalidOperationException();
                loaded.RemoveItem(loaded.Items.First().Id, Now.AddMinutes(1));
            });
        }
        await using (var verify = fixture.Create())
        {
            Assert.Single((await new OrderingStore(verify).CartAsync(cart.Id, default))!.Items);
        }

        await using (var db = fixture.Create())
        {
            var store = new OrderingStore(db);
            await Unit(db).ExecuteInTransactionAsync(async ct =>
            {
                var loaded = await store.CartAsync(cart.Id, ct) ?? throw new InvalidOperationException();
                loaded.Clear(Now.AddMinutes(2));
            });
        }
        await using var empty = fixture.Create();
        Assert.Empty((await new OrderingStore(empty).CartAsync(cart.Id, default))!.Items);
        Assert.Equal(0, await empty.Set<CartItem>().CountAsync(item => EF.Property<Guid>(item, "CartId") == cart.Id));
    }

    [OrderingPostgresFact]
    public async Task OrderLinesAndSnapshotsCannotBeChangedAfterPlacement()
    {
        var order = await AddOrderAsync();
        await using var db = fixture.Create();
        var store = new OrderingStore(db);

        var lineError = await Assert.ThrowsAsync<InvalidOperationException>(() => Unit(db).ExecuteInTransactionAsync(async ct =>
        {
            var loaded = await store.OrderAsync(order.Id, ct) ?? throw new InvalidOperationException();
            db.Entry(loaded.Lines.Single()).Property(nameof(OrderLine.FinalUnitPrice)).CurrentValue = 1m;
        }));

        Assert.Equal("Order lines are immutable.", lineError.Message);
        store.Reset();
        var snapshotError = await Assert.ThrowsAsync<InvalidOperationException>(() => Unit(db).ExecuteInTransactionAsync(async ct =>
        {
            var loaded = await store.OrderAsync(order.Id, ct) ?? throw new InvalidOperationException();
            db.Entry(loaded).Property(nameof(Order.Buyer)).CurrentValue = loaded.Buyer with { Name = "Changed" };
        }));
        Assert.Equal("Order snapshots are immutable.", snapshotError.Message);
    }

    [OrderingPostgresFact]
    public async Task UnitOfWorkRollsBackAndTranslatesUniqueAndConcurrencyConflicts()
    {
        var rolledBack = Attempt("rollback-owner", "rollback-key", Guid.NewGuid());
        await using (var db = fixture.Create())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Unit(db).ExecuteInTransactionAsync(_ =>
            {
                new OrderingStore(db).Add(rolledBack);
                throw new InvalidOperationException("stop");
            }));
        }
        await using (var verify = fixture.Create())
        {
            Assert.False(await verify.Attempts.AnyAsync(value => value.Id == rolledBack.Id));
        }

        var first = Attempt("unique-owner", "same-key", Guid.NewGuid());
        await AddAttemptAsync(first);
        var duplicate = Attempt(first.OwnerKey, first.IdempotencyKey, Guid.NewGuid());
        await using (var db = fixture.Create())
        {
            var conflict = await Assert.ThrowsAsync<OrderingFailure>(() => Unit(db).ExecuteInTransactionAsync(_ =>
            {
                new OrderingStore(db).Add(duplicate);
                return Task.CompletedTask;
            }));
            Assert.Equal((409, "OrderingConflict"), (conflict.Status, conflict.Code));
        }

        await using var left = fixture.Create();
        await using var right = fixture.Create();
        var leftAttempt = await left.Attempts.SingleAsync(value => value.Id == first.Id);
        var rightAttempt = await right.Attempts.SingleAsync(value => value.Id == first.Id);
        var oldVersion = leftAttempt.Version;
        leftAttempt.UpdatedAt = Now.AddMinutes(1);
        await Unit(left).ExecuteInTransactionAsync(_ => Task.CompletedTask);
        Assert.NotEqual(oldVersion, leftAttempt.Version);
        rightAttempt.UpdatedAt = Now.AddMinutes(2);
        var stale = await Assert.ThrowsAsync<OrderingFailure>(() => Unit(right).ExecuteInTransactionAsync(_ => Task.CompletedTask));
        Assert.Equal((409, "OrderingConflict"), (stale.Status, stale.Code));
    }

    [OrderingPostgresFact]
    public async Task StoreUsesSequenceAndReturnsOnlyRunnableWorkInCreationOrder()
    {
        await using var db = fixture.Create();
        var store = new OrderingStore(db);
        string first = "";
        string second = "";
        var expected = new List<Guid>();
        await Unit(db).ExecuteInTransactionAsync(async ct =>
        {
            first = await store.NextOrderNumberAsync(ct);
            second = await store.NextOrderNumberAsync(ct);
            for (var index = 0; index < 23; index++)
            {
                var attempt = Attempt("queue-" + Guid.NewGuid(), "key", Guid.NewGuid());
                attempt.CreatedAt = Now.AddSeconds(index);
                if (index == 0)
                {
                    attempt.Stage = CheckoutStage.Completed;
                }
                else if (index == 1)
                {
                    attempt.LeaseUntil = Now.AddMinutes(1);
                }
                else
                {
                    expected.Add(attempt.Id);
                }
                store.Add(attempt);
            }
        });

        Assert.StartsWith("ORD-", first);
        Assert.Equal(14, first.Length);
        Assert.Equal(long.Parse(first[4..]) + 1, long.Parse(second[4..]));
        Assert.Equal(expected.Take(20), await store.PendingAttemptsAsync(Now, default));
    }

    [OrderingPostgresFact]
    public async Task MigrationMatchesModelAndOwnsEveryForeignKey()
    {
        await using var db = fixture.Create();
        Assert.Equal(await db.Database.GetAppliedMigrationsAsync(), db.Database.GetMigrations());
        Assert.False(db.Database.HasPendingModelChanges());
        var foreignSchemas = await db.Database.SqlQueryRaw<string>("SELECT DISTINCT target_ns.nspname::text AS \"Value\" FROM pg_constraint c JOIN pg_class source ON source.oid = c.conrelid JOIN pg_namespace source_ns ON source_ns.oid = source.relnamespace JOIN pg_class target ON target.oid = c.confrelid JOIN pg_namespace target_ns ON target_ns.oid = target.relnamespace WHERE c.contype = 'f' AND source_ns.nspname = 'ordering'").ToArrayAsync();
        Assert.All(foreignSchemas, schema => Assert.Equal("ordering", schema));
    }

    private async Task<Order> AddOrderAsync()
    {
        await using var db = fixture.Create();
        var store = new OrderingStore(db);
        Order? order = null;
        await Unit(db).ExecuteInTransactionAsync(async ct =>
        {
            order = Order.Place(Guid.NewGuid(), await store.NextOrderNumberAsync(ct), null, "owner", null, "hash", OrderType.Retail,
                "Storefront", Buyer(), Address(), null, new PaymentTerm("COD", null), Guid.NewGuid(), Guid.NewGuid(),
                Now.AddMinutes(15), "VND", [Line()], Now);
            store.Add(order);
        });
        return order!;
    }

    private async Task AddAttemptAsync(CheckoutAttempt attempt)
    {
        await using var db = fixture.Create();
        await Unit(db).ExecuteInTransactionAsync(_ =>
        {
            new OrderingStore(db).Add(attempt);
            return Task.CompletedTask;
        });
    }

    private static OrderingUnitOfWork Unit(OrderingDbContext db) => new(db, new NullDispatcher());
    private static string Json<T>(T value) => JsonSerializer.Serialize(value);

    private static CheckoutAttempt Attempt(string owner, string key, Guid orderId)
    {
        var item = Line();
        var input = new CheckoutInput(null, "VND", OrderType.Retail, Buyer(), Address(), null, new PaymentTerm("COD", null),
            [new RequestedLine(item.ProductItemId, item.Quantity)], null, null, null);
        var quote = new PriceQuote(Guid.NewGuid(), Now, "VND",
            [new QuoteLine(item.ProductItemId, item.Quantity, 120m, 120m, 0m, 240m, "PriceList", Guid.NewGuid(), null, null, null, null, null)],
            240m, 0m, 0m, 0m, 240m);
        return new CheckoutAttempt
        {
            OwnerKey = owner,
            GuestTokenHash = "hash",
            IdempotencyKey = key,
            Fingerprint = Guid.NewGuid().ToString("N"),
            OrderId = orderId,
            Input = input,
            CatalogItems = [new CatalogItem(item.ProductItemId, item.ProductId, item.SkuCode, item.ProductName,
                item.VariantDescription, item.ImageUrl, true, null, false, [])],
            Quote = quote,
            StockLines = [new StockLine(item.ProductItemId, item.Quantity)],
            ExpiresAt = Now.AddMinutes(15),
            ReservationId = Guid.NewGuid(),
            Stage = CheckoutStage.Quoted,
            Result = new CheckoutResult(Guid.NewGuid(), "Quoted", null, null, null, "VND", null, null, Now.AddMinutes(15)),
            CreatedAt = Now,
            UpdatedAt = Now
        };
    }

    private static BuyerSnapshot Buyer() => new("Guest", null, null, null, "Buyer", "buyer@example.com", "0900000000");
    private static AddressSnapshot Address() => new("Buyer", "0900000000", "Street", null, null, null, null, null, "VN");
    private static OrderLineSnapshot Line() => new(Guid.NewGuid(), Guid.NewGuid(), "Product", "SKU-1", "Black", null, 2,
        new PriceSnapshot(120m, 120m, 0m, "VND", "PriceList", Guid.NewGuid(), null, null, null, null, null));

    private sealed class NullDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}