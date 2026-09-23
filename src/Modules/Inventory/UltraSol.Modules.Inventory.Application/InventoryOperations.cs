using FluentValidation;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Modules.Inventory.Application.Validation;
using UltraSol.Modules.Inventory.Domain;
using UltraSol.Modules.Inventory.Domain.Adjustments;
using UltraSol.Modules.Inventory.Domain.Repositories;
using UltraSol.Modules.Inventory.Domain.Reservations;
using UltraSol.Modules.Inventory.Domain.Stocks;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.IntegrationEvents.Inventory;

namespace UltraSol.Modules.Inventory.Application;

/// <summary>Transaction-neutral business operations; the entry point owns the Inventory unit of work.</summary>
public sealed class InventoryOperations(IInventoryStockRepository stocks, IStockReservationRepository reservations, IInventoryAdjustmentRepository adjustments, IStockMovementRepository movements, IInventoryOutbox outbox, TimeProvider time)
{
    public async Task<ReceiptResult> ReceiveAsync(ReceiveStockRequest request, string? actorId, CancellationToken ct)
    {
        await new ReceiveStockRequestValidator().ValidateAndThrowAsync(request, ct);
        var replay = await FindReceiptReplayAsync(request, ct);
        if (replay is not null)
        {
            return replay;
        }
        var now = time.GetUtcNow();
        var stock = await GetStockAsync(request.ProductItemId, true, ct);
        stock.Receive(request.Quantity);
        Touch(stock, actorId, now);
        var movement = new StockMovement(request.OperationId, InventoryDefaults.WarehouseId, request.ProductItemId, MovementType.GoodsReceipt,
            request.Quantity, request.ReferenceType, request.ReferenceId, request.Note, actorId, now);
        await movements.AddAsync(movement, ct);
        return Receipt(movement);
    }

    public async Task<ReceiptResult?> FindReceiptReplayAsync(ReceiveStockRequest request, CancellationToken ct)
    {
        var movement = await movements.FindAsync(request.OperationId, ct);
        if (movement is null)
        {
            return null;
        }
        if (movement.Type != MovementType.GoodsReceipt || movement.WarehouseId != InventoryDefaults.WarehouseId || movement.ProductItemId != request.ProductItemId ||
            movement.QuantityDelta != request.Quantity || movement.ReferenceType != request.ReferenceType.Trim() || movement.ReferenceId != request.ReferenceId || movement.Note != Normalize(request.Note))
        {
            throw new DomainException("OperationId already belongs to a different receipt.", "IdempotencyConflict");
        }
        return Receipt(movement);
    }

    public async Task<ReservationResult> ReserveAsync(ReserveStockRequest request, string? actorId, CancellationToken ct)
    {
        await new ReserveStockRequestValidator().ValidateAndThrowAsync(request, ct);
        var replay = await FindReservationReplayAsync(request, ct);
        if (replay is not null)
        {
            return replay;
        }
        var now = time.GetUtcNow();
        if (InventoryTime.ToUtc(request.ExpiresAt) <= now)
        {
            throw new ValidationException([new FluentValidation.Results.ValidationFailure(nameof(request.ExpiresAt), "ExpiresAt must be in the future for a new reservation.")]);
        }
        var reservation = new StockReservation(request.OrderId, request.ExpiresAt, ReservationLines(request), now);
        foreach (var line in reservation.Lines.OrderBy(line => line.ProductItemId))
        {
            var stock = await GetStockAsync(line.ProductItemId, false, ct);
            stock.Reserve(line.Quantity);
            Touch(stock, actorId, now);
        }
        Touch(reservation, actorId, now);
        await reservations.AddAsync(reservation, ct);
        return Reservation(reservation);
    }

    public async Task<ReservationResult?> FindReservationReplayAsync(ReserveStockRequest request, CancellationToken ct)
    {
        var reservation = await reservations.FindByOrderAsync(request.OrderId, ct);
        if (reservation is null)
        {
            return null;
        }
        if (!reservation.Matches(request.ExpiresAt, ReservationLines(request)))
        {
            throw new DomainException("OrderId already belongs to a different reservation.", "IdempotencyConflict");
        }
        if (reservation.EffectiveStatus(time.GetUtcNow()) == ReservationStatus.Expired)
        {
            throw new DomainException("Reservation has expired.", "ReservationExpired");
        }
        return Reservation(reservation);
    }

    public async Task<ReservationResult> ReleaseByOrderAsync(Guid orderId, string? actorId, CancellationToken ct)
    {
        RequireId(orderId, nameof(orderId));
        var reservation = await reservations.FindByOrderAsync(orderId, ct)
            ?? throw EntityNotFoundException.For<StockReservation>(orderId);
        return await ReleaseAsync(reservation, actorId, ct);
    }

    public async Task<ReservationResult> ReleaseAsync(Guid reservationId, string? actorId, CancellationToken ct)
    {
        RequireId(reservationId, nameof(reservationId));
        return await ReleaseAsync(await reservations.GetTrackedRequiredAsync(reservationId, ct), actorId, ct);
    }

    private async Task<ReservationResult> ReleaseAsync(StockReservation reservation, string? actorId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        if (reservation.EffectiveStatus(now) == ReservationStatus.Expired)
        {
            await ExpireAsync(reservation, actorId, now, ct);
            return Reservation(reservation);
        }
        var changed = reservation.Release(now);
        if (changed)
        {
            await ReleaseLinesAsync(reservation, actorId, now, ct);
        }
        return Reservation(reservation);
    }

    public async Task<ReservationResult> IssueAsync(Guid reservationId, string? actorId, CancellationToken ct)
    {
        RequireId(reservationId, nameof(reservationId));
        var reservation = await reservations.GetTrackedRequiredAsync(reservationId, ct);
        var now = time.GetUtcNow();
        if (reservation.Consume(now))
        {
            foreach (var line in reservation.Lines.OrderBy(line => line.ProductItemId))
            {
                var stock = await GetStockAsync(line.ProductItemId, false, ct);
                stock.IssueReserved(line.Quantity);
                Touch(stock, actorId, now);
                await movements.AddAsync(new StockMovement(Guid.CreateVersion7(), line.WarehouseId, line.ProductItemId, MovementType.OrderIssue,
                    -line.Quantity, "Reservation", reservation.Id, null, actorId, now), ct);
            }
            Touch(reservation, actorId, now);
        }
        return Reservation(reservation);
    }

    public async Task<bool> ExpireAsync(Guid reservationId, CancellationToken ct)
    {
        var reservation = await reservations.FindTrackedAsync(reservationId, ct);
        var now = time.GetUtcNow();
        return reservation is not null && await ExpireAsync(reservation, "inventory-expiry", now, ct);
    }

    private async Task<bool> ExpireAsync(StockReservation reservation, string? actorId, DateTimeOffset now, CancellationToken ct)
    {
        if (!reservation.Expire(now))
        {
            return false;
        }
        await ReleaseLinesAsync(reservation, actorId, now, ct);
        outbox.Add(new InventoryReservationExpiredV1(Guid.CreateVersion7(), reservation.OrderId, 1, now,
            reservation.Id, reservation.OrderId, reservation.ExpiredAt!.Value));
        return true;
    }

    private async Task ReleaseLinesAsync(StockReservation reservation, string? actorId, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var line in reservation.Lines.OrderBy(line => line.ProductItemId))
        {
            var stock = await GetStockAsync(line.ProductItemId, false, ct);
            stock.Release(line.Quantity);
            Touch(stock, actorId, now);
        }
        Touch(reservation, actorId, now);
    }

    public async Task<ReservationResult?> FindReservationTerminalReplayAsync(Guid id, Guid? orderId, bool issued, CancellationToken ct)
    {
        var reservation = orderId.HasValue
            ? await reservations.FindByOrderAsync(orderId.Value, ct)
            : await reservations.FindTrackedAsync(id, ct);
        if (reservation is null)
        {
            return null;
        }
        if (issued ? reservation.Status == ReservationStatus.Consumed : reservation.Status is ReservationStatus.Released or ReservationStatus.Expired)
        {
            return Reservation(reservation);
        }
        return null;
    }

    public async Task<AdjustmentResult> CreateAdjustmentAsync(CreateAdjustmentRequest request, string? actorId, CancellationToken ct)
    {
        await new CreateAdjustmentRequestValidator().ValidateAndThrowAsync(request, ct);
        var adjustment = new InventoryAdjustment(Enum.Parse<AdjustmentReason>(request.Reason, true), request.Note, AdjustmentLines(request.Lines));
        Touch(adjustment, actorId, time.GetUtcNow());
        await adjustments.AddAsync(adjustment, ct);
        return Adjustment(adjustment);
    }

    public async Task<AdjustmentResult> UpdateAdjustmentAsync(Guid adjustmentId, UpdateAdjustmentRequest request, string? actorId, CancellationToken ct)
    {
        RequireId(adjustmentId, nameof(adjustmentId));
        await new UpdateAdjustmentRequestValidator().ValidateAndThrowAsync(request, ct);
        var adjustment = await adjustments.GetTrackedRequiredAsync(adjustmentId, ct);
        if (adjustment.ConcurrencyStamp != request.ConcurrencyStamp)
        {
            throw new ConcurrencyException(nameof(InventoryAdjustment), adjustmentId);
        }
        adjustment.Update(Enum.Parse<AdjustmentReason>(request.Reason, true), request.Note, AdjustmentLines(request.Lines));
        Touch(adjustment, actorId, time.GetUtcNow());
        return Adjustment(adjustment);
    }

    public async Task<AdjustmentResult> PostAdjustmentAsync(Guid adjustmentId, string? actorId, CancellationToken ct)
    {
        RequireId(adjustmentId, nameof(adjustmentId));
        var adjustment = await adjustments.GetTrackedRequiredAsync(adjustmentId, ct);
        var now = time.GetUtcNow();
        if (adjustment.Post(now))
        {
            foreach (var line in adjustment.Lines.OrderBy(line => line.ProductItemId))
            {
                var stock = await GetStockAsync(line.ProductItemId, line.QuantityDelta > 0, ct);
                stock.Adjust(line.QuantityDelta);
                Touch(stock, actorId, now);
                await movements.AddAsync(new StockMovement(Guid.CreateVersion7(), line.WarehouseId, line.ProductItemId, MovementType.Adjustment,
                    line.QuantityDelta, "Adjustment", adjustment.Id, adjustment.Note, actorId, now), ct);
            }
            Touch(adjustment, actorId, now);
        }
        return Adjustment(adjustment);
    }

    public async Task<AdjustmentResult> CancelAdjustmentAsync(Guid adjustmentId, string? actorId, CancellationToken ct)
    {
        RequireId(adjustmentId, nameof(adjustmentId));
        var adjustment = await adjustments.GetTrackedRequiredAsync(adjustmentId, ct);
        if (adjustment.Cancel(time.GetUtcNow()))
        {
            Touch(adjustment, actorId, time.GetUtcNow());
        }
        return Adjustment(adjustment);
    }

    public async Task<AdjustmentResult?> FindAdjustmentTerminalReplayAsync(Guid id, bool posted, CancellationToken ct)
    {
        var adjustment = await adjustments.FindTrackedAsync(id, ct);
        return adjustment is not null && adjustment.Status == (posted ? AdjustmentStatus.Posted : AdjustmentStatus.Cancelled)
            ? Adjustment(adjustment)
            : null;
    }

    private async Task<InventoryStock> GetStockAsync(Guid productItemId, bool create, CancellationToken ct)
    {
        var stock = await stocks.FindByKeyAsync(InventoryDefaults.WarehouseId, productItemId, ct);
        if (stock is not null)
        {
            return stock;
        }
        if (!create)
        {
            throw new DomainException("Insufficient inventory.", "InsufficientInventory");
        }
        stock = new InventoryStock(InventoryDefaults.WarehouseId, productItemId);
        await stocks.AddAsync(stock, ct);
        return stock;
    }

    private static IEnumerable<ReservationLine> ReservationLines(ReserveStockRequest request) =>
        request.Lines.Select(line => new ReservationLine(InventoryDefaults.WarehouseId, line.ProductItemId, line.Quantity));

    private static IEnumerable<AdjustmentLine> AdjustmentLines(IEnumerable<AdjustmentLineRequest> lines) =>
        lines.Select(line => new AdjustmentLine(InventoryDefaults.WarehouseId, line.ProductItemId, line.QuantityDelta));

    private static ReceiptResult Receipt(StockMovement movement) =>
        new(movement.Id, movement.WarehouseId, movement.ProductItemId, movement.QuantityDelta, movement.OccurredAt);

    private ReservationResult Reservation(StockReservation reservation) => new(reservation.Id, reservation.OrderId, reservation.EffectiveStatus(time.GetUtcNow()).ToString(),
        reservation.ExpiresAt, reservation.Lines.OrderBy(line => line.ProductItemId).Select(line => new ReservationLineResult(line.WarehouseId, line.ProductItemId, line.Quantity)).ToArray());

    private static AdjustmentResult Adjustment(InventoryAdjustment adjustment) => new(adjustment.Id, adjustment.Status.ToString(), adjustment.ConcurrencyStamp);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Touch(AggregateRoot root, string? actorId, DateTimeOffset now)
    {
        if (root.CreatedAt == default)
        {
            root.MarkCreated(actorId, now);
        }
        else
        {
            root.MarkUpdated(actorId, now);
        }
    }

    private static void RequireId(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            throw new ValidationException(name + " must not be empty.");
        }
    }
}