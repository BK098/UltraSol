using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Domain.Orders;
using UltraSol.Modules.Ordering.Domain.Repositories;
using UltraSol.Modules.Ordering.Domain.ValueObjects;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Ordering.Application.Features.Orders;

public sealed record OrderOperationResult(Guid OperationId, Guid OrderId, string State, string? ErrorCode);

public sealed class OrderLifecycle(IOrderingStore store, IOrderingUnitOfWork unit, OrderingAccess access, IInventoryReservationClient inventory, TimeProvider clock,
    UltraSol.Modules.Ordering.Application.Messaging.IOrderingOutbox outbox)
{
    public async Task<ApiResult<OrderOperationResult>> CancelAsync(Guid orderId, string stamp, string reason, string? secret, bool admin, CancellationToken ct)
    {
        reason = OrderingRule.Text(reason, 2000, "Reason");
        store.Reset();
        var operationId = await unit.ExecuteInTransactionAsync(async token =>
        {
            await store.LockAsync("order:" + orderId, token);
            var order = await store.OrderAsync(orderId, token) ?? throw new OrderingFailure(404, "NotFound", "Order was not found.");
            if (!admin)
            {
                access.Demand(order.OwnerKey, order.GuestTokenHash, secret);
            }
            var existing = await store.FindOperationAsync(orderId, "Cancel", token);
            if (existing is not null)
            {
                if (existing.Reason != reason)
                {
                    throw new OrderingFailure(409, "CancellationConflict", "A different cancellation reason is already recorded.");
                }
                return existing.Id;
            }
            if (order.ConcurrencyStamp != stamp || order.Status is not (OrderStatus.Placed or OrderStatus.Confirmed))
            {
                throw new OrderingFailure(409, "OrderConcurrencyConflict", "Order changed or cannot be cancelled.");
            }
            var operation = new OrderOperation { OrderId = orderId, Reason = reason, ReservationId = order.InventoryReservationId,
                CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow() };
            store.Add(operation);
            return operation.Id;
        }, ct);
        await RunAsync(operationId, ct);
        store.Reset();
        var result = (await store.OperationAsync(operationId, ct))!;
        if (result.State == "Failed")
        {
            throw new OrderingFailure(409, result.ErrorCode ?? "CancellationFailed", "Cancellation could not be completed; inspect the operation error code.");
        }
        return ApiResultBuilder.Success(new OrderOperationResult(result.Id, result.OrderId, result.State, result.ErrorCode), statusCode: result.State == "Completed" ? 200 : 202);
    }

    public async Task RunAsync(Guid operationId, CancellationToken ct)
    {
        var lease = Guid.NewGuid();
        store.Reset();
        var operation = await unit.ExecuteInTransactionAsync(async token =>
        {
            var peek = await store.OperationAsync(operationId, token);
            if (peek is null)
            {
                return null;
            }
            await store.LockAsync("order:" + peek.OrderId, token);
            if (peek.State != "Pending" || peek.LeaseUntil > clock.GetUtcNow())
            {
                return null;
            }
            peek.LeaseToken = lease;
            peek.LeaseUntil = clock.GetUtcNow().AddSeconds(60);
            return peek;
        }, ct);
        if (operation is null)
        {
            return;
        }
        Reservation? reservation;
        try
        {
            reservation = await inventory.FindAsync(operation.OrderId, ct);
            if (reservation is null)
            {
                return;
            }
            RequireReference(operation, reservation);
            if (operation.Kind is "Cancel" or "Reject" && reservation.Status != "Consumed")
            {
                reservation = await inventory.ReleaseAsync(operation.OrderId, ct);
            }
            RequireReference(operation, reservation);
        }
        catch (OrderingFailure error)
        {
            if (error.Code == "ReservationMismatch")
            {
                store.Reset();
                await unit.ExecuteInTransactionAsync(async token =>
                {
                    await store.LockAsync("order:" + operation.OrderId, token);
                    var current = await store.OperationAsync(operationId, token);
                    if (current is not null && current.LeaseToken == lease && current.State == "Pending")
                    {
                        current.State = "Failed";
                        current.ErrorCode = error.Code;
                        current.UpdatedAt = clock.GetUtcNow();
                        current.LeaseToken = null;
                        current.LeaseUntil = null;
                    }
                }, ct);
            }
            return;
        }
        store.Reset();
        await unit.ExecuteInTransactionAsync(async token =>
        {
            await store.LockAsync("order:" + operation.OrderId, token);
            var current = await store.OperationAsync(operationId, token);
            if (current is null || current.LeaseToken != lease || current.State != "Pending")
            {
                return;
            }
            var order = await store.OrderAsync(current.OrderId, token) ?? throw new InvalidOperationException("Order operation owner missing.");
            if (current.Kind == "Complete")
            {
                if (order.Status == OrderStatus.Completed)
                {
                    current.State = "Completed";
                }
                else if (reservation.Status == "Consumed" && order.Status == OrderStatus.Confirmed)
                {
                    order.Complete(current.CompletedAt ?? clock.GetUtcNow());
                    outbox.Changed(order);
                    current.State = "Completed";
                }
                else if (reservation.Status is "Expired" or "Released" || order.Status is OrderStatus.Cancelled or OrderStatus.Rejected)
                {
                    current.State = "Failed";
                    current.ErrorCode = "FulfillmentConflict";
                }
            }
            else if (reservation.Status is "Expired" or "Released")
            {
                if (order.Status is OrderStatus.Placed or OrderStatus.Confirmed)
                {
                    if (current.Kind == "Reject" && order.Status == OrderStatus.Placed)
                    {
                        order.Reject(current.Reason!, clock.GetUtcNow());
                    }
                    else
                    {
                        order.Cancel(current.Reason!, clock.GetUtcNow());
                    }
                    outbox.Changed(order);
                }
                current.State = order.Status is OrderStatus.Cancelled or OrderStatus.Rejected ? "Completed" : "Failed";
            }
            else if (reservation.Status == "Consumed")
            {
                current.State = "Failed";
                current.ErrorCode = "ReservationConsumed";
            }
            current.UpdatedAt = clock.GetUtcNow();
            current.LeaseToken = null;
            current.LeaseUntil = null;
        }, ct);
    }

    private static void RequireReference(OrderOperation operation, Reservation reservation)
    {
        if (reservation.OrderId != operation.OrderId || reservation.Id != operation.ReservationId)
        {
            throw new OrderingFailure(409, "ReservationMismatch", "Inventory returned a different reservation.");
        }
    }
}
