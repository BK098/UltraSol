using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Inventory.Domain.Adjustments;

public sealed class AdjustmentLine : BaseEntity
{
    private AdjustmentLine() { }

    public AdjustmentLine(Guid warehouseId, Guid productItemId, int quantityDelta)
    {
        WarehouseId = Guard.Id(warehouseId);
        ProductItemId = Guard.Id(productItemId);
        if (quantityDelta == 0)
        {
            throw new DomainException("Adjustment quantity cannot be zero.", "InvalidQuantity");
        }
        QuantityDelta = quantityDelta;
    }

    public Guid WarehouseId { get; private set; }
    public Guid ProductItemId { get; private set; }
    public int QuantityDelta { get; private set; }
}
