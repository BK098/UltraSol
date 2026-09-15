using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Inventory.Domain.Adjustments;
using UltraSol.Modules.Inventory.Domain.Repositories;
using UltraSol.Modules.Inventory.Domain.Reservations;
using UltraSol.Modules.Inventory.Domain.Stocks;
using UltraSol.Modules.Inventory.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Inventory.Infrastructure.Repositories;

public sealed class InventoryStockRepository(InventoryDbContext context) : Repository<InventoryStock>(context), IInventoryStockRepository
{
    public async Task<InventoryStock?> FindByKeyAsync(Guid warehouseId, Guid productItemId, CancellationToken cancellationToken = default)
    {
        var stock = await context.Stocks.SingleOrDefaultAsync(value =>
            value.WarehouseId == warehouseId && value.ProductItemId == productItemId, cancellationToken);
        if (stock is not null)
        {
            await context.MaterializeAsync(stock, cancellationToken);
        }
        return stock;
    }
}

public sealed class StockReservationRepository(InventoryDbContext context) : Repository<StockReservation>(context), IStockReservationRepository
{
    public async Task<StockReservation?> FindByOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var reservation = await context.Reservations.SingleOrDefaultAsync(value => value.OrderId == orderId, cancellationToken);
        if (reservation is not null)
        {
            await context.MaterializeAsync(reservation, cancellationToken);
        }
        return reservation;
    }
}

public sealed class InventoryAdjustmentRepository(InventoryDbContext context) : Repository<InventoryAdjustment>(context), IInventoryAdjustmentRepository;

public sealed class StockMovementRepository(InventoryDbContext context) : IStockMovementRepository
{
    public Task<StockMovement?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Movements.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);

    public async Task AddAsync(StockMovement movement, CancellationToken cancellationToken = default)
    {
        await context.Movements.AddAsync(movement, cancellationToken);
    }
}

public sealed class InventoryUnitOfWork(InventoryDbContext context, IDomainEventDispatcher domainEventDispatcher)
    : UnitOfWork(context, domainEventDispatcher), IInventoryUnitOfWork;
