using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using UltraSol.Modules.Inventory.Application;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Modules.Inventory.Application.Reads;
using UltraSol.Modules.Inventory.Domain;
using UltraSol.Modules.Inventory.Domain.Adjustments;
using UltraSol.Modules.Inventory.Domain.Repositories;
using UltraSol.Modules.Inventory.Domain.Reservations;
using UltraSol.Modules.Inventory.Domain.Stocks;
using UltraSol.Modules.Inventory.Infrastructure;
using UltraSol.Modules.Inventory.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.IntegrationEvents.Inventory;
using UltraSol.Shared.Infrastructure.Messaging;
using Xunit;

namespace UltraSol.Modules.Inventory.Tests;

public sealed class InventoryPersistenceTests(InventoryDatabaseFixture fixture) : IClassFixture<InventoryDatabaseFixture>
{
    [InventoryPostgresFact]
    public async Task ReceiptReplayChangesStockOnceAndRejectsDifferentPayload()
    {
        await using var services = fixture.Services();
        var module = Module(services);
        var request = Receipt(Guid.NewGuid(), 8);
        var first = await module.ReceiveAsync(request, "receiver");
        Assert.Equal(first, await module.ReceiveAsync(request, "retry-user"));
        await Assert.ThrowsAnyAsync<DomainException>(() => module.ReceiveAsync(request with { Quantity = 9 }, "receiver"));
        await Assert.ThrowsAnyAsync<DomainException>(() => module.ReceiveAsync(request with { Note = "changed" }, "receiver"));
        var stocks = await module.GetAvailabilityAsync([request.ProductItemId, Guid.NewGuid()]);
        Assert.Equal((8, 0, 8), (stocks[0].OnHand, stocks[0].Reserved, stocks[0].Available));
        Assert.Equal(0, stocks[1].OnHand);
        await using var db = fixture.Create();
        var row = await db.Movements.SingleAsync(movement => movement.Id == request.OperationId);
        Assert.Equal("receiver", row.ActorId);
        Assert.Equal(1, await db.Movements.CountAsync(movement => movement.ProductItemId == request.ProductItemId));
    }

    [InventoryPostgresFact]
    public async Task MissingLastSkuRollsBackEntireReservation()
    {
        await using var services = fixture.Services();
        var module = Module(services);
        var receipt = Receipt(Guid.NewGuid(), 4);
        await module.ReceiveAsync(receipt, "actor");
        var request = Reserve([new(receipt.ProductItemId, 2), new(Guid.NewGuid(), 1)]);
        await Assert.ThrowsAnyAsync<DomainException>(() => module.ReserveAsync(request, "actor"));
        Assert.Equal(0, (await module.GetAvailabilityAsync([receipt.ProductItemId]))[0].Reserved);
        await using var db = fixture.Create();
        Assert.False(await db.Reservations.AnyAsync(reservation => reservation.OrderId == request.OrderId));
    }

    [InventoryPostgresFact]
    public async Task ConcurrentReservationsCannotOversellTheLastUnit()
    {
        var item = Guid.NewGuid();
        await using (var seed = fixture.Services())
        {
            await Module(seed).ReceiveAsync(Receipt(item, 1), "actor");
        }
        await using var services = BarrierServices("stock_balances");
        var module = Module(services);
        var results = await Task.WhenAll(Attempt(() => module.ReserveAsync(Reserve([new(item, 1)]), "a")), Attempt(() => module.ReserveAsync(Reserve([new(item, 1)]), "b")));
        Assert.Single(results, result => result.Error is null);
        Assert.IsAssignableFrom<DomainException>(Assert.Single(results, result => result.Error is not null).Error);
        Assert.Equal(1, (await module.GetAvailabilityAsync([item]))[0].Reserved);
    }

    [InventoryPostgresFact]
    public async Task ConcurrentSameReceiptAndFirstStockRecoverOneCommittedResult()
    {
        await using var services = BarrierServices("stock_balances");
        var module = Module(services);
        var request = Receipt(Guid.NewGuid(), 3);
        var results = await Task.WhenAll(module.ReceiveAsync(request, "a"), module.ReceiveAsync(request, "b"));
        Assert.Equal(results[0], results[1]);
        Assert.Equal(3, (await module.GetAvailabilityAsync([request.ProductItemId]))[0].OnHand);
        await using var db = fixture.Create();
        Assert.Equal(1, await db.Movements.CountAsync(movement => movement.ProductItemId == request.ProductItemId));
    }

    [InventoryPostgresFact]
    public async Task ConcurrentDifferentReceiptsCreatingSameStockLeaveOneCommittedReceipt()
    {
        await using var services = BarrierServices("stock_balances");
        var module = Module(services);
        var item = Guid.NewGuid();
        var results = await Task.WhenAll(Attempt(() => module.ReceiveAsync(Receipt(item, 3), "a")), Attempt(() => module.ReceiveAsync(Receipt(item, 3), "b")));
        Assert.Single(results, result => result.Error is null);
        Assert.IsAssignableFrom<DomainException>(Assert.Single(results, result => result.Error is not null).Error);
        Assert.Equal(3, (await module.GetAvailabilityAsync([item]))[0].OnHand);
    }

    [InventoryPostgresFact]
    public async Task SameOrderRaceAndPayloadMismatchNeverReserveTwice()
    {
        var item = Guid.NewGuid();
        await using (var seed = fixture.Services())
        {
            await Module(seed).ReceiveAsync(Receipt(item, 6), "actor");
        }
        await using var services = BarrierServices("stock_reservations");
        var module = Module(services);
        var request = Reserve([new(item, 2)]);
        var results = await Task.WhenAll(module.ReserveAsync(request, "a"), module.ReserveAsync(request, "b"));
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Equal(2, (await module.GetAvailabilityAsync([item]))[0].Reserved);
        await Assert.ThrowsAnyAsync<DomainException>(() => module.ReserveAsync(request with { Lines = [new(item, 3)] }, "a"));
    }

    [InventoryPostgresFact]
    public async Task SameOrderConcurrentDifferentPayloadHasOneWinner()
    {
        var item = Guid.NewGuid();
        await using (var seed = fixture.Services())
        {
            await Module(seed).ReceiveAsync(Receipt(item, 8), "actor");
        }
        await using var services = BarrierServices("stock_reservations");
        var module = Module(services);
        var request = Reserve([new(item, 2)]);
        var results = await Task.WhenAll(Attempt(() => module.ReserveAsync(request, "a")), Attempt(() => module.ReserveAsync(request with { Lines = [new(item, 3)] }, "b")));
        var winner = Assert.Single(results, result => result.Error is null).Value!;
        Assert.IsAssignableFrom<DomainException>(Assert.Single(results, result => result.Error is not null).Error);
        Assert.Equal(winner.Lines.Single().Quantity, (await module.GetAvailabilityAsync([item]))[0].Reserved);
    }

    [InventoryPostgresFact]
    public async Task IssueVersusReleaseHasOneStateAndOnePhysicalOutcome()
    {
        var item = Guid.NewGuid();
        ReservationResult reservation;
        await using (var seed = fixture.Services())
        {
            var module = Module(seed);
            await module.ReceiveAsync(Receipt(item, 4), "actor");
            reservation = await module.ReserveAsync(Reserve([new(item, 2)]), "actor");
        }
        await using var services = BarrierServices("stock_reservations");
        var runtime = Module(services);
        var results = await Task.WhenAll(Attempt(() => runtime.IssueAsync(reservation.Id, "ship")), Attempt(() => runtime.ReleaseAsync(reservation.Id, "cancel")));
        Assert.Single(results, result => result.Error is null);
        await using var db = fixture.Create();
        var state = await db.Reservations.SingleAsync(value => value.Id == reservation.Id);
        var balance = (await runtime.GetAvailabilityAsync([item]))[0];
        Assert.Equal(0, balance.Reserved);
        Assert.Equal(state.Status == ReservationStatus.Consumed ? 2 : 4, balance.OnHand);
        Assert.Equal(state.Status == ReservationStatus.Consumed ? 2 : 1, await db.Movements.CountAsync(value => value.ProductItemId == item));
    }

    [InventoryPostgresFact]
    public async Task ExpiredReservationCannotBeIssuedOrReservedAgainAndWorkerReleasesOnce()
    {
        var now = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new InventoryTestClock(now);
        await using var services = fixture.Services(clock);
        var module = Module(services);
        var item = Guid.NewGuid();
        await module.ReceiveAsync(Receipt(item, 4), "actor");
        var request = new ReserveStockRequest(Guid.NewGuid(), now.AddMinutes(1), [new(item, 2)]);
        var reservation = await module.ReserveAsync(request, "actor");
        clock.Now = request.ExpiresAt;
        await Assert.ThrowsAnyAsync<DomainException>(() => module.IssueAsync(reservation.Id, "ship"));
        await Assert.ThrowsAnyAsync<DomainException>(() => module.ReserveAsync(request, "retry"));
        var worker = new ReservationExpiryWorker(services.GetRequiredService<IServiceScopeFactory>(), clock, NullLogger<ReservationExpiryWorker>.Instance);
        await worker.SweepAsync();
        await using var db = fixture.Create();
        var expired = await db.Reservations.AsNoTracking().SingleAsync(value => value.Id == reservation.Id);
        Assert.Equal(ReservationStatus.Expired, expired.Status);
        Assert.Equal(clock.Now, expired.ExpiredAt);
        Assert.Equal("inventory-expiry", expired.UpdatedBy);
        await worker.SweepAsync();
        var replay = await db.Reservations.AsNoTracking().SingleAsync(value => value.Id == reservation.Id);
        Assert.Equal(expired.ConcurrencyStamp, replay.ConcurrencyStamp);
        Assert.Equal((4, 0), ((await module.GetAvailabilityAsync([item]))[0].OnHand, (await module.GetAvailabilityAsync([item]))[0].Reserved));
        Assert.Equal(1, await db.Movements.CountAsync(value => value.ProductItemId == item));
        var outbox = Assert.Single(await db.Set<OutboxMessage>().ToArrayAsync(),
            value => JsonSerializer.Deserialize<InventoryReservationExpiredV1>(value.Payload)!.ReservationId == reservation.Id);
        var message = JsonSerializer.Deserialize<InventoryReservationExpiredV1>(outbox.Payload)!;
        Assert.Equal(reservation.Id, message.ReservationId);
        Assert.Equal(reservation.OrderId, message.OrderId);
        Assert.Equal(clock.Now, message.ExpiredAt);
        Assert.Equal(clock.Now, message.OccurredAt);
        Assert.Equal(reservation.OrderId, message.CorrelationId);
        Assert.Equal(1, message.ContractVersion);
        Assert.Equal(outbox.Id, message.EventId);
    }

    [InventoryPostgresFact]
    public async Task DueReleasePersistsOneExpiryEventAndRollsBackWithStock()
    {
        var now = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new InventoryTestClock(now);
        await using var services = fixture.Services(clock);
        var module = Module(services);
        var item = Guid.NewGuid();
        await module.ReceiveAsync(Receipt(item, 4), "actor");
        var reservation = await module.ReserveAsync(new(Guid.NewGuid(), now.AddMinutes(1), [new(item, 2)]), "actor");
        clock.Now = reservation.ExpiresAt;

        await using (var failed = services.CreateAsyncScope())
        {
            var operations = failed.ServiceProvider.GetRequiredService<InventoryOperations>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => failed.ServiceProvider.GetRequiredService<IInventoryUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await operations.ReleaseAsync(reservation.Id, "cancel", ct);
                throw new InvalidOperationException("Injected failure after expiry.");
            }));
        }

        Assert.Equal(2, (await module.GetAvailabilityAsync([item]))[0].Reserved);
        await using (var beforeCommit = fixture.Create())
        {
            Assert.Equal(ReservationStatus.Active, (await beforeCommit.Reservations.SingleAsync(value => value.Id == reservation.Id)).Status);
            Assert.DoesNotContain(await beforeCommit.Set<OutboxMessage>().ToArrayAsync(),
                value => JsonSerializer.Deserialize<InventoryReservationExpiredV1>(value.Payload)!.ReservationId == reservation.Id);
        }

        var result = await module.ReleaseAsync(reservation.Id, "cancel");
        Assert.Equal(nameof(ReservationStatus.Expired), result.Status);
        await module.ReleaseAsync(reservation.Id, "retry");

        await using var db = fixture.Create();
        Assert.Equal(0, (await module.GetAvailabilityAsync([item]))[0].Reserved);
        var outbox = Assert.Single(await db.Set<OutboxMessage>().ToArrayAsync(),
            value => JsonSerializer.Deserialize<InventoryReservationExpiredV1>(value.Payload)!.ReservationId == reservation.Id);
        var message = JsonSerializer.Deserialize<InventoryReservationExpiredV1>(outbox.Payload)!;
        Assert.Equal(reservation.Id, message.ReservationId);
        Assert.Equal(reservation.OrderId, message.OrderId);
        Assert.Equal(clock.Now, message.ExpiredAt);
    }

    [InventoryPostgresFact]
    public async Task ExpiryAndReleaseRaceReleaseReservedExactlyOnce()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new InventoryTestClock(now);
        var item = Guid.NewGuid();
        ReservationResult reservation;
        await using (var seed = fixture.Services(clock))
        {
            var module = Module(seed);
            await module.ReceiveAsync(Receipt(item, 4), "actor");
            reservation = await module.ReserveAsync(new(Guid.NewGuid(), now.AddMinutes(1), [new(item, 3)]), "actor");
        }
        clock.Now = now.AddMinutes(1);
        await using var services = BarrierServices("stock_reservations", clock);
        async Task<bool> Expire()
        {
            await using var scope = services.CreateAsyncScope();
            var operations = scope.ServiceProvider.GetRequiredService<InventoryOperations>();
            return await scope.ServiceProvider.GetRequiredService<IInventoryUnitOfWork>().ExecuteInTransactionAsync(ct => operations.ExpireAsync(reservation.Id, ct));
        }
        var expiry = Attempt(Expire);
        var release = Attempt(() => Module(services).ReleaseAsync(reservation.Id, "cancel"));
        await Task.WhenAll(expiry, release);
        Assert.True(expiry.Result.Error is null || release.Result.Error is null);
        if (expiry.Result.Error is not null)
        {
            Assert.IsType<DbUpdateConcurrencyException>(expiry.Result.Error);
        }
        if (release.Result.Error is not null)
        {
            Assert.IsAssignableFrom<DomainException>(release.Result.Error);
        }
        Assert.Equal(0, (await Module(services).GetAvailabilityAsync([item]))[0].Reserved);
        await using var db = fixture.Create();
        Assert.Equal(ReservationStatus.Expired, (await db.Reservations.SingleAsync(value => value.Id == reservation.Id)).Status);
    }

    [InventoryPostgresFact]
    public async Task DraftUpdateReplacesLinesAndRejectsStaleVersion()
    {
        await using var services = fixture.Services();
        var module = Module(services);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var draft = await module.CreateAdjustmentAsync(new("ManualCorrection", "old", [new(first, 2)]), "actor");
        var request = new UpdateAdjustmentRequest(draft.ConcurrencyStamp, "Recovered", "updated", [new(first, 3), new(second, 1)]);
        var updated = await module.UpdateAdjustmentAsync(draft.Id, request, "editor");
        Assert.NotEqual(draft.ConcurrencyStamp, updated.ConcurrencyStamp);
        await Assert.ThrowsAnyAsync<DomainException>(() => module.UpdateAdjustmentAsync(draft.Id, request, "stale-editor"));
        updated = await module.UpdateAdjustmentAsync(draft.Id, request with { ConcurrencyStamp = updated.ConcurrencyStamp, Lines = [new(second, 5)] }, "editor");
        await using var db = fixture.Create();
        var row = await db.Adjustments.Include(value => value.Lines).SingleAsync(value => value.Id == draft.Id);
        Assert.Equal(updated.ConcurrencyStamp, row.ConcurrencyStamp);
        Assert.Equal(second, Assert.Single(row.Lines).ProductItemId);
        Assert.Equal(5, row.Lines.Single().QuantityDelta);
        Assert.Equal("editor", row.UpdatedBy);
    }

    [InventoryPostgresFact]
    public async Task AdjustmentRollsBackEveryLineWhenReservedStockWouldBeLost()
    {
        await using var services = fixture.Services();
        var module = Module(services);
        var item = Guid.NewGuid();
        var created = Guid.NewGuid();
        await module.ReceiveAsync(Receipt(item, 5), "actor");
        await module.ReserveAsync(Reserve([new(item, 4)]), "actor");
        var draft = await module.CreateAdjustmentAsync(new("Lost", null, [new(created, 2), new(item, -2)]), "actor");
        await Assert.ThrowsAnyAsync<DomainException>(() => module.PostAdjustmentAsync(draft.Id, "actor"));
        var balance = await module.GetAvailabilityAsync([item, created]);
        Assert.Equal((5, 4), (balance[0].OnHand, balance[0].Reserved));
        Assert.Equal(0, balance[1].OnHand);
        await using var db = fixture.Create();
        Assert.Equal(AdjustmentStatus.Draft, (await db.Adjustments.SingleAsync(value => value.Id == draft.Id)).Status);
        Assert.False(await db.Movements.AnyAsync(value => value.ReferenceId == draft.Id));
    }

    [InventoryPostgresFact]
    public async Task ConcurrentPostAndReplayHaveOneLedgerEntryAndPostedIsImmutable()
    {
        var item = Guid.NewGuid();
        AdjustmentResult draft;
        await using (var seed = fixture.Services())
        {
            draft = await Module(seed).CreateAdjustmentAsync(new("Recovered", null, [new(item, 7)]), "actor");
        }
        await using var services = BarrierServices("inventory_adjustments");
        var module = Module(services);
        var results = await Task.WhenAll(module.PostAdjustmentAsync(draft.Id, "a"), module.PostAdjustmentAsync(draft.Id, "b"));
        Assert.All(results, result => Assert.Equal("Posted", result.Status));
        Assert.Equal(results[0].ConcurrencyStamp, results[1].ConcurrencyStamp);
        Assert.Equal(results[0], await module.PostAdjustmentAsync(draft.Id, "retry"));
        await Assert.ThrowsAnyAsync<DomainException>(() => module.UpdateAdjustmentAsync(draft.Id, new(results[0].ConcurrencyStamp, "Lost", null, [new(item, -1)]), "actor"));
        Assert.Equal(7, (await module.GetAvailabilityAsync([item]))[0].OnHand);
        await using var db = fixture.Create();
        Assert.Equal(1, await db.Movements.CountAsync(value => value.ReferenceId == draft.Id));
    }

    [InventoryPostgresFact]
    public async Task PhysicalLedgerReconcilesAndReleaseIssueReplaysDoNotDuplicateIt()
    {
        await using var services = fixture.Services();
        var module = Module(services);
        var item = Guid.NewGuid();
        await module.ReceiveAsync(Receipt(item, 10), "actor");
        var cancelled = await module.ReserveAsync(Reserve([new(item, 2)]), "actor");
        await module.ReleaseByOrderAsync(cancelled.OrderId, "actor");
        await module.ReleaseAsync(cancelled.Id, "retry");
        var shipped = await module.ReserveAsync(Reserve([new(item, 3)]), "actor");
        await module.IssueAsync(shipped.Id, "actor");
        await module.IssueAsync(shipped.Id, "retry");
        await Assert.ThrowsAnyAsync<DomainException>(() => module.ReleaseAsync(shipped.Id, "actor"));
        var adjustment = await module.CreateAdjustmentAsync(new("Damaged", "broken", [new(item, -1)]), "actor");
        await module.PostAdjustmentAsync(adjustment.Id, "actor");
        await using var db = fixture.Create();
        var balance = (await module.GetAvailabilityAsync([item]))[0];
        Assert.Equal((6, 0, 6), (balance.OnHand, balance.Reserved, balance.Available));
        Assert.Equal(balance.OnHand, await db.Movements.Where(value => value.ProductItemId == item).SumAsync(value => value.QuantityDelta));
        Assert.Equal(3, await db.Movements.CountAsync(value => value.ProductItemId == item));
        await using var scope = services.CreateAsyncScope();
        var reads = scope.ServiceProvider.GetRequiredService<IInventoryReadStore>();
        var movements = await reads.MovementsAsync(PaginationRequest.Default, item, "Adjustment", adjustment.Id, "Adjustment", null, null, default);
        Assert.Equal(-1, Assert.Single(movements.Items).QuantityDelta);
        Assert.Equal(1, (await reads.StocksAsync(new PaginationRequest { Search = item.ToString() }, default)).TotalCount);
    }

    [InventoryPostgresFact]
    public async Task LedgerRejectsSqlAndEfMutationsAndDatabaseEnforcesBalanceChecks()
    {
        await using var services = fixture.Services();
        var request = Receipt(Guid.NewGuid(), 2);
        await Module(services).ReceiveAsync(request, "actor");
        await using var db = fixture.Create();
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.stock_movements SET quantity_delta = 100 WHERE id = {request.OperationId}"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM inventory.stock_movements WHERE id = {request.OperationId}"));
        var check = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.stock_balances SET reserved = 3 WHERE product_item_id = {request.ProductItemId}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, check.SqlState);
        var movement = await db.Movements.SingleAsync(value => value.Id == request.OperationId);
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Entry(movement).Property(value => value.QuantityDelta).CurrentValue = 10;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(movement).State = EntityState.Deleted;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await transaction.RollbackAsync();
    }

    [InventoryPostgresFact]
    public async Task MigrationSeedsOneIdOnlyWarehouseAndHasNoCrossSchemaForeignKeys()
    {
        await using var db = fixture.Create();
        var warehouses = await db.Set<Dictionary<string, object>>("InventoryWarehouse").AsNoTracking().ToArrayAsync();
        Assert.Equal(InventoryDefaults.WarehouseId, Assert.Single(warehouses)["Id"]);
        Assert.Single(warehouses[0]);
        Assert.Equal(await db.Database.GetAppliedMigrationsAsync(), db.Database.GetMigrations());
        var foreignSchemas = await db.Database.SqlQueryRaw<string>("SELECT DISTINCT target_ns.nspname::text AS \"Value\" FROM pg_constraint c JOIN pg_class source ON source.oid = c.conrelid JOIN pg_namespace source_ns ON source_ns.oid = source.relnamespace JOIN pg_class target ON target.oid = c.confrelid JOIN pg_namespace target_ns ON target_ns.oid = target.relnamespace WHERE c.contype = 'f' AND source_ns.nspname = 'inventory'").ToArrayAsync();
        Assert.All(foreignSchemas, schema => Assert.Equal("inventory", schema));
    }

    private ServiceProvider BarrierServices(string table, TimeProvider? clock = null)
    {
        var barrier = new TwoReadBarrier(table);
        return fixture.Services(clock, services => services.ConfigureDbContext<InventoryDbContext>(options => options.AddInterceptors(barrier)));
    }

    private static IInventoryModule Module(ServiceProvider services) => new InventoryModuleService(services.GetRequiredService<IServiceScopeFactory>());
    private static ReceiveStockRequest Receipt(Guid item, int quantity) => new(Guid.NewGuid(), item, quantity, "GoodsReceipt", Guid.NewGuid(), null);
    private static ReserveStockRequest Reserve(IReadOnlyList<StockLineRequest> lines) => new(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(1), lines);

    private static async Task<(T? Value, Exception? Error)> Attempt<T>(Func<Task<T>> action)
    {
        try
        {
            return (await action(), null);
        }
        catch (Exception error)
        {
            return (default, error);
        }
    }

    private sealed class TwoReadBarrier(string table) : DbCommandInterceptor
    {
        private int _arrivals;
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) && command.CommandText.Contains("inventory." + table, StringComparison.Ordinal))
            {
                var number = Interlocked.Increment(ref _arrivals);
                if (number == 2)
                {
                    _release.TrySetResult();
                }
                if (number <= 2)
                {
                    await _release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                }
            }
            return result;
        }
    }
}