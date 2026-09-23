using System.Globalization;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Domain.Carts;
using UltraSol.Modules.Ordering.Domain.Orders;
using UltraSol.Modules.Ordering.Infrastructure.Persistence;

namespace UltraSol.Modules.Ordering.Infrastructure.Checkout;

public sealed class OrderingStore(OrderingDbContext context) : IOrderingStore
{
    public void Reset() => context.ChangeTracker.Clear();

    public async Task LockAsync(string key, CancellationToken ct)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Ordering locks require an active transaction.");
        }
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", ct);
    }

    public async Task<ShoppingCart?> CartAsync(Guid id, CancellationToken ct)
    {
        var cart = await context.Carts.SingleOrDefaultAsync(value => value.Id == id, ct);
        if (cart is not null)
        {
            await context.MaterializeAsync(cart, ct);
        }
        return cart;
    }

    public async Task<Order?> OrderAsync(Guid id, CancellationToken ct)
    {
        var order = await context.Orders.SingleOrDefaultAsync(value => value.Id == id, ct);
        if (order is not null)
        {
            await context.MaterializeAsync(order, ct);
        }
        return order;
    }

    public Task<CheckoutAttempt?> AttemptAsync(Guid id, CancellationToken ct) =>
        context.Attempts.SingleOrDefaultAsync(value => value.Id == id, ct);

    public Task<CheckoutAttempt?> FindAttemptAsync(string ownerKey, string key, CancellationToken ct) =>
        context.Attempts.SingleOrDefaultAsync(value => value.OwnerKey == ownerKey && value.IdempotencyKey == key, ct);

    public Task<CheckoutAttempt?> AttemptByOrderAsync(Guid orderId, CancellationToken ct) =>
        context.Attempts.SingleOrDefaultAsync(value => value.OrderId == orderId, ct);

    public Task<OrderOperation?> OperationAsync(Guid id, CancellationToken ct) =>
        context.Operations.SingleOrDefaultAsync(value => value.Id == id, ct);

    public Task<OrderOperation?> FindOperationAsync(Guid orderId, string kind, CancellationToken ct) =>
        context.Operations.SingleOrDefaultAsync(value => value.OrderId == orderId && value.Kind == kind, ct);

    public Task<Guid[]> PendingAttemptsAsync(DateTimeOffset now, CancellationToken ct) => context.Attempts.AsNoTracking()
        .Where(value => value.Stage != CheckoutStage.Completed && value.Stage != CheckoutStage.Failed &&
            (value.LeaseUntil == null || value.LeaseUntil <= now))
        .OrderBy(value => value.CreatedAt).Take(20).Select(value => value.Id).ToArrayAsync(ct);

    public Task<Guid[]> PendingOperationsAsync(DateTimeOffset now, CancellationToken ct) => context.Operations.AsNoTracking()
        .Where(value => value.State == "Pending" && (value.LeaseUntil == null || value.LeaseUntil <= now))
        .OrderBy(value => value.CreatedAt).Take(20).Select(value => value.Id).ToArrayAsync(ct);

    public async Task<string> NextOrderNumberAsync(CancellationToken ct)
    {
        var value = await context.Database.SqlQueryRaw<long>("SELECT nextval('ordering.order_number_seq') AS \"Value\"").SingleAsync(ct);
        return "ORD-" + value.ToString("D10", CultureInfo.InvariantCulture);
    }

    public void Add(ShoppingCart cart) => context.Carts.Add(cart);
    public void Add(Order order) => context.Orders.Add(order);
    public void Add(CheckoutAttempt attempt) => context.Attempts.Add(attempt);
    public void Add(OrderOperation operation) => context.Operations.Add(operation);
}