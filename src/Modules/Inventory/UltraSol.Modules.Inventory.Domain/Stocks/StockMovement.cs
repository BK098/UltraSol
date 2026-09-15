using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Inventory.Domain.Stocks;

public enum MovementType
{
    GoodsReceipt,
    OrderIssue,
    Adjustment
}

public sealed class StockMovement : BaseEntity
{
    private StockMovement() { ReferenceType = null!; }

    public StockMovement(Guid id, Guid warehouseId, Guid productItemId, MovementType type, int quantityDelta,
        string referenceType, Guid referenceId, string? note, string? actorId, DateTimeOffset occurredAt)
        : base(Guard.Id(id))
    {
        WarehouseId = Guard.Id(warehouseId);
        ProductItemId = Guard.Id(productItemId);
        if (!Enum.IsDefined(type))
        {
            throw new DomainException("Movement type is invalid.");
        }
        ValidateQuantity(type, quantityDelta);
        Type = type;
        QuantityDelta = quantityDelta;
        ReferenceType = Required(referenceType, "Reference type", 100);
        ReferenceId = Guard.Id(referenceId);
        Note = Optional(note, "Note", 2000);
        ActorId = Optional(actorId, "Actor", 256);
        OccurredAt = InventoryTime.ToUtc(occurredAt);
    }

    public Guid WarehouseId { get; private set; }
    public Guid ProductItemId { get; private set; }
    public MovementType Type { get; private set; }
    public int QuantityDelta { get; private set; }
    public string ReferenceType { get; private set; }
    public Guid ReferenceId { get; private set; }
    public string? Note { get; private set; }
    public string? ActorId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private static void ValidateQuantity(MovementType type, int quantityDelta)
    {
        if (quantityDelta == 0 || type == MovementType.GoodsReceipt && quantityDelta < 0 ||
            type == MovementType.OrderIssue && quantityDelta > 0)
        {
            throw new DomainException("Movement quantity has an invalid direction.", "InvalidQuantity");
        }
    }

    private static string Required(string value, string field, int maximumLength)
    {
        var normalized = Guard.Required(value, field);
        if (normalized.Length > maximumLength)
        {
            throw new DomainException($"{field} cannot exceed {maximumLength} characters.");
        }
        return normalized;
    }

    private static string? Optional(string? value, string field, int maximumLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maximumLength)
        {
            throw new DomainException($"{field} cannot exceed {maximumLength} characters.");
        }
        return normalized;
    }
}
