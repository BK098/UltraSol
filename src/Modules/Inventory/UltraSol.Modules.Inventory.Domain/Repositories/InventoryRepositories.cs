using UltraSol.Modules.Inventory.Domain.Adjustments;
using UltraSol.Modules.Inventory.Domain.Reservations;
using UltraSol.Modules.Inventory.Domain.Stocks;
using UltraSol.Shared.Domain.Common.Repositories;

namespace UltraSol.Modules.Inventory.Domain.Repositories;

public interface IInventoryStockRepository : IRepository<InventoryStock>
{
    Task<InventoryStock?> FindByKeyAsync(
        Guid warehouseId,
        Guid productItemId,
        CancellationToken cancellationToken = default);
}

public interface IStockReservationRepository : IRepository<StockReservation>
{
    Task<StockReservation?> FindByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}

public interface IInventoryAdjustmentRepository : IRepository<InventoryAdjustment>;

public interface IStockMovementRepository
{
    Task<StockMovement?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(StockMovement movement, CancellationToken cancellationToken = default);
}

public interface IInventoryUnitOfWork : IUnitOfWork;
