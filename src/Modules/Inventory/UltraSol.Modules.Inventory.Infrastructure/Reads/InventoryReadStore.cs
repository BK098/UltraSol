using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Inventory.Application.Features.Adjustments.Queries;
using UltraSol.Modules.Inventory.Application.Features.Movements.Queries;
using UltraSol.Modules.Inventory.Application.Features.Reservations.Queries;
using UltraSol.Modules.Inventory.Application.Features.Stocks.Queries;
using UltraSol.Modules.Inventory.Application.Reads;
using UltraSol.Modules.Inventory.Domain;
using UltraSol.Modules.Inventory.Domain.Adjustments;
using UltraSol.Modules.Inventory.Domain.Reservations;
using UltraSol.Modules.Inventory.Domain.Stocks;
using UltraSol.Modules.Inventory.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Inventory.Infrastructure.Reads;

public sealed class InventoryReadStore(InventoryDbContext db, TimeProvider time) : IInventoryReadStore
{
    public Task<PaginatedResult<GetStocksQuery.Response>> StocksAsync(PaginationRequest page, CancellationToken ct)
    {
        var query = db.Stocks.AsNoTracking().Where(stock => stock.WarehouseId == InventoryDefaults.WarehouseId);
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            query = Guid.TryParse(page.Search, out var id) ? query.Where(stock => stock.ProductItemId == id) : query.Where(stock => false);
        }
        return PageAsync(query.OrderBy(stock => stock.ProductItemId)
            .Select(stock => new GetStocksQuery.Response(stock.WarehouseId, stock.ProductItemId, stock.OnHand, stock.Reserved, stock.OnHand - stock.Reserved)), page, ct);
    }

    public async Task<GetStockQuery.Response> StockAsync(Guid productItemId, CancellationToken ct) =>
        await db.Stocks.AsNoTracking().Where(stock => stock.WarehouseId == InventoryDefaults.WarehouseId && stock.ProductItemId == productItemId)
            .Select(stock => new GetStockQuery.Response(stock.WarehouseId, stock.ProductItemId, stock.OnHand, stock.Reserved, stock.OnHand - stock.Reserved)).SingleOrDefaultAsync(ct)
        ?? new GetStockQuery.Response(InventoryDefaults.WarehouseId, productItemId, 0, 0, 0);

    public async Task<GetReservationQuery.Response?> ReservationAsync(Guid id, CancellationToken ct)
    {
        var reservation = await db.Reservations.AsNoTracking().Include(reservation => reservation.Lines).SingleOrDefaultAsync(reservation => reservation.Id == id, ct);
        return reservation is null ? null : Reservation(reservation);
    }

    public async Task<GetReservationQuery.Response?> ReservationByOrderAsync(Guid orderId, CancellationToken ct)
    {
        var reservation = await db.Reservations.AsNoTracking().Include(reservation => reservation.Lines).SingleOrDefaultAsync(reservation => reservation.OrderId == orderId, ct);
        return reservation is null ? null : Reservation(reservation);
    }

    public Task<PaginatedResult<GetAdjustmentsQuery.Response>> AdjustmentsAsync(PaginationRequest page, string? status, CancellationToken ct)
    {
        var query = db.Adjustments.AsNoTracking();
        if (status is not null)
        {
            var parsed = Enum.Parse<AdjustmentStatus>(status, true);
            query = query.Where(adjustment => adjustment.Status == parsed);
        }
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var search = page.Search.Trim();
            query = Guid.TryParse(search, out var id)
                ? query.Where(adjustment => adjustment.Id == id || adjustment.Note != null && adjustment.Note.Contains(search))
                : query.Where(adjustment => adjustment.Note != null && adjustment.Note.Contains(search));
        }
        return PageAsync(query.OrderByDescending(adjustment => adjustment.CreatedAt).ThenBy(adjustment => adjustment.Id)
            .Select(adjustment => new GetAdjustmentsQuery.Response(adjustment.Id, adjustment.Reason.ToString(), adjustment.Note, adjustment.Status.ToString(), adjustment.CreatedAt)), page, ct);
    }

    public async Task<GetAdjustmentQuery.Response?> AdjustmentAsync(Guid id, CancellationToken ct)
    {
        var adjustment = await db.Adjustments.AsNoTracking().Include(adjustment => adjustment.Lines).SingleOrDefaultAsync(adjustment => adjustment.Id == id, ct);
        return adjustment is null ? null : new(adjustment.Id, adjustment.Reason.ToString(), adjustment.Note, adjustment.Status.ToString(), adjustment.ConcurrencyStamp,
            adjustment.CreatedAt, adjustment.PostedAt, adjustment.CancelledAt,
            adjustment.Lines.OrderBy(line => line.ProductItemId).Select(line => new GetAdjustmentQuery.Line(line.WarehouseId, line.ProductItemId, line.QuantityDelta)).ToArray());
    }

    public Task<PaginatedResult<GetMovementsQuery.Response>> MovementsAsync(PaginationRequest page, Guid? productItemId, string? movementType, Guid? referenceId,
        string? referenceType, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var query = db.Movements.AsNoTracking().Where(movement => movement.WarehouseId == InventoryDefaults.WarehouseId);
        if (productItemId.HasValue)
        {
            query = query.Where(movement => movement.ProductItemId == productItemId.Value);
        }
        if (movementType is not null)
        {
            var parsed = Enum.Parse<MovementType>(movementType, true);
            query = query.Where(movement => movement.Type == parsed);
        }
        if (referenceId.HasValue)
        {
            query = query.Where(movement => movement.ReferenceId == referenceId.Value);
        }
        if (!string.IsNullOrWhiteSpace(referenceType))
        {
            query = query.Where(movement => movement.ReferenceType == referenceType.Trim());
        }
        if (from.HasValue)
        {
            var utc = from.Value.ToUniversalTime();
            query = query.Where(movement => movement.OccurredAt >= utc);
        }
        if (to.HasValue)
        {
            var utc = to.Value.ToUniversalTime();
            query = query.Where(movement => movement.OccurredAt <= utc);
        }
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var search = page.Search.Trim();
            query = query.Where(movement => movement.ReferenceType.Contains(search) || movement.Note != null && movement.Note.Contains(search));
        }
        return PageAsync(query.OrderByDescending(movement => movement.OccurredAt).ThenBy(movement => movement.Id)
            .Select(movement => new GetMovementsQuery.Response(movement.Id, movement.WarehouseId, movement.ProductItemId, movement.Type.ToString(), movement.QuantityDelta,
                movement.ReferenceType, movement.ReferenceId, movement.Note, movement.ActorId, movement.OccurredAt)), page, ct);
    }

    public async Task<IReadOnlyList<Guid>> ExpiredReservationIdsAsync(DateTimeOffset now, int limit, CancellationToken ct) =>
        await db.Reservations.AsNoTracking().Where(reservation => reservation.Status == ReservationStatus.Active && reservation.ExpiresAt <= now)
            .OrderBy(reservation => reservation.ExpiresAt).ThenBy(reservation => reservation.Id).Select(reservation => reservation.Id).Take(Math.Clamp(limit, 1, 100)).ToArrayAsync(ct);

    private GetReservationQuery.Response Reservation(StockReservation reservation) => new(reservation.Id, reservation.OrderId,
        reservation.EffectiveStatus(time.GetUtcNow()).ToString(), reservation.ExpiresAt, reservation.CreatedAt, reservation.ReleasedAt, reservation.ConsumedAt, reservation.ExpiredAt,
        reservation.Lines.OrderBy(line => line.ProductItemId).Select(line => new GetReservationQuery.Line(line.WarehouseId, line.ProductItemId, line.Quantity)).ToArray());

    private static async Task<PaginatedResult<T>> PageAsync<T>(IQueryable<T> query, PaginationRequest page, CancellationToken ct)
    {
        var skip = ((long)page.PageNumber - 1) * page.Take;
        if (skip is < 0 or > int.MaxValue)
        {
            throw new ValidationException("PageIndex is outside the supported range.");
        }
        var count = await query.CountAsync(ct);
        var items = await query.Skip((int)skip).Take(page.Take).ToArrayAsync(ct);
        return PaginatedResult<T>.Create(items, count, page);
    }
}