namespace UltraSol.Modules.Inventory.Application.Contracts;

public sealed record StockLineRequest(Guid ProductItemId, int Quantity);
public sealed record AdjustmentLineRequest(Guid ProductItemId, int QuantityDelta);
public sealed record ReceiveStockRequest(Guid OperationId, Guid ProductItemId, int Quantity, string ReferenceType, Guid ReferenceId, string? Note);
public sealed record ReserveStockRequest(Guid OrderId, DateTimeOffset ExpiresAt, IReadOnlyList<StockLineRequest> Lines);
public sealed record CreateAdjustmentRequest(string Reason, string? Note, IReadOnlyList<AdjustmentLineRequest> Lines);
public sealed record UpdateAdjustmentRequest(string ConcurrencyStamp, string Reason, string? Note, IReadOnlyList<AdjustmentLineRequest> Lines);
public sealed record ReceiptResult(Guid OperationId, Guid WarehouseId, Guid ProductItemId, int Quantity, DateTimeOffset OccurredAt);
public sealed record ReservationLineResult(Guid WarehouseId, Guid ProductItemId, int Quantity);
public sealed record ReservationResult(Guid Id, Guid OrderId, string Status, DateTimeOffset ExpiresAt, IReadOnlyList<ReservationLineResult> Lines);
public sealed record AdjustmentResult(Guid Id, string Status, string ConcurrencyStamp);
public sealed record AvailabilityResult(Guid WarehouseId, Guid ProductItemId, int OnHand, int Reserved, int Available);

/// <summary>Trusted in-process boundary. HTTP callers use permission-protected CQRS endpoints.</summary>
public interface IInventoryModule
{
    Task<ReceiptResult> ReceiveAsync(ReceiveStockRequest request, string? actorId, CancellationToken ct = default);
    Task<ReservationResult> ReserveAsync(ReserveStockRequest request, string? actorId, CancellationToken ct = default);
    Task<ReservationResult> ReleaseByOrderAsync(Guid orderId, string? actorId, CancellationToken ct = default);
    Task<ReservationResult> ReleaseAsync(Guid reservationId, string? actorId, CancellationToken ct = default);
    Task<ReservationResult> IssueAsync(Guid reservationId, string? actorId, CancellationToken ct = default);
    Task<AdjustmentResult> CreateAdjustmentAsync(CreateAdjustmentRequest request, string? actorId, CancellationToken ct = default);
    Task<AdjustmentResult> UpdateAdjustmentAsync(Guid adjustmentId, UpdateAdjustmentRequest request, string? actorId, CancellationToken ct = default);
    Task<AdjustmentResult> PostAdjustmentAsync(Guid adjustmentId, string? actorId, CancellationToken ct = default);
    Task<AdjustmentResult> CancelAdjustmentAsync(Guid adjustmentId, string? actorId, CancellationToken ct = default);
    Task<IReadOnlyList<AvailabilityResult>> GetAvailabilityAsync(IReadOnlyList<Guid> productItemIds, CancellationToken ct = default);
}
