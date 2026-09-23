using UltraSol.Modules.Ordering.Domain.Carts;
using UltraSol.Modules.Ordering.Domain.Orders;

namespace UltraSol.Modules.Ordering.Application.Checkout;

public interface IOrderingStore
{
    void Reset();
    Task LockAsync(string key, CancellationToken ct);
    Task<ShoppingCart?> CartAsync(Guid id, CancellationToken ct);
    Task<Order?> OrderAsync(Guid id, CancellationToken ct);
    Task<CheckoutAttempt?> AttemptAsync(Guid id, CancellationToken ct);
    Task<CheckoutAttempt?> FindAttemptAsync(string ownerKey, string key, CancellationToken ct);
    Task<CheckoutAttempt?> AttemptByOrderAsync(Guid orderId, CancellationToken ct);
    Task<OrderOperation?> OperationAsync(Guid id, CancellationToken ct);
    Task<OrderOperation?> FindOperationAsync(Guid orderId, string kind, CancellationToken ct);
    Task<Guid[]> PendingAttemptsAsync(DateTimeOffset now, CancellationToken ct);
    Task<Guid[]> PendingOperationsAsync(DateTimeOffset now, CancellationToken ct);
    Task<string> NextOrderNumberAsync(CancellationToken ct);
    void Add(ShoppingCart cart);
    void Add(Order order);
    void Add(CheckoutAttempt attempt);
    void Add(OrderOperation operation);
}