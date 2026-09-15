using UltraSol.Modules.Inventory.Application.Features.Adjustments.Queries;
using UltraSol.Modules.Inventory.Application.Features.Movements.Queries;
using UltraSol.Modules.Inventory.Application.Features.Reservations.Queries;
using UltraSol.Modules.Inventory.Application.Features.Stocks.Queries;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Inventory.Application.Reads;

public interface IInventoryReadStore
{
    Task<PaginatedResult<GetStocksQuery.Response>> StocksAsync(PaginationRequest page, CancellationToken ct);
    Task<GetStockQuery.Response> StockAsync(Guid productItemId, CancellationToken ct);
    Task<GetReservationQuery.Response?> ReservationAsync(Guid id, CancellationToken ct);
    Task<GetReservationQuery.Response?> ReservationByOrderAsync(Guid orderId, CancellationToken ct);
    Task<PaginatedResult<GetAdjustmentsQuery.Response>> AdjustmentsAsync(PaginationRequest page, string? status, CancellationToken ct);
    Task<GetAdjustmentQuery.Response?> AdjustmentAsync(Guid id, CancellationToken ct);
    Task<PaginatedResult<GetMovementsQuery.Response>> MovementsAsync(PaginationRequest page, Guid? productItemId,
        string? movementType, Guid? referenceId, string? referenceType, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ExpiredReservationIdsAsync(DateTimeOffset now, int limit, CancellationToken ct);
}