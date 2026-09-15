using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Inventory.Domain.Reservations;

public sealed class ReservationLine : BaseEntity
{
    private ReservationLine() { }

    public ReservationLine(Guid warehouseId, Guid productItemId, int quantity)
    {
        WarehouseId = Guard.Id(warehouseId);
        ProductItemId = Guard.Id(productItemId);
        if (quantity <= 0)
        {
            throw new DomainException("Reservation quantity must be positive.", "InvalidQuantity");
        }
        Quantity = quantity;
    }

    public Guid WarehouseId { get; private set; }
    public Guid ProductItemId { get; private set; }
    public int Quantity { get; private set; }
}
