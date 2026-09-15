using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Inventory.Domain.Adjustments;

public enum AdjustmentStatus
{
    Draft,
    Posted,
    Cancelled
}

public enum AdjustmentReason
{
    PhysicalCountMismatch,
    Damaged,
    Lost,
    Recovered,
    ManualCorrection,
    AdministrativeCorrection
}

public sealed class InventoryAdjustment : AggregateRoot
{
    private readonly List<AdjustmentLine> _lines = [];

    private InventoryAdjustment() { }

    public InventoryAdjustment(AdjustmentReason reason, string? note, IEnumerable<AdjustmentLine> lines)
    {
        Apply(reason, note, lines);
        Status = AdjustmentStatus.Draft;
    }

    public AdjustmentReason Reason { get; private set; }
    public string? Note { get; private set; }
    public AdjustmentStatus Status { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public IReadOnlyCollection<AdjustmentLine> Lines => _lines.AsReadOnly();

    public void Update(AdjustmentReason reason, string? note, IEnumerable<AdjustmentLine> lines)
    {
        if (Status != AdjustmentStatus.Draft)
        {
            throw new DomainException("Only draft adjustments can be updated.");
        }
        Apply(reason, note, lines);
    }

    public bool Post(DateTimeOffset now)
    {
        if (Status == AdjustmentStatus.Posted)
        {
            return false;
        }
        if (Status == AdjustmentStatus.Cancelled)
        {
            throw new DomainException("Cancelled adjustments cannot be posted.");
        }
        Status = AdjustmentStatus.Posted;
        PostedAt = now.ToUniversalTime();
        return true;
    }

    public bool Cancel(DateTimeOffset now)
    {
        if (Status == AdjustmentStatus.Cancelled)
        {
            return false;
        }
        if (Status == AdjustmentStatus.Posted)
        {
            throw new DomainException("Posted adjustments cannot be cancelled.");
        }
        Status = AdjustmentStatus.Cancelled;
        CancelledAt = now.ToUniversalTime();
        return true;
    }

    public override void MarkDeleted(string? actorId, DateTimeOffset utcNow)
    {
        throw new DomainException("Inventory adjustments cannot be deleted.");
    }

    public override void Restore()
    {
        throw new DomainException("Inventory adjustments cannot be restored.");
    }

    private void Apply(AdjustmentReason reason, string? note, IEnumerable<AdjustmentLine> lines)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new DomainException("Adjustment reason is invalid.");
        }
        var values = ValidateLines(lines);
        var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (normalizedNote?.Length > 2000)
        {
            throw new DomainException("Adjustment note cannot exceed 2000 characters.");
        }
        Reason = reason;
        Note = normalizedNote;
        _lines.Clear();
        _lines.AddRange(values);
    }

    private static AdjustmentLine[] ValidateLines(IEnumerable<AdjustmentLine> lines)
    {
        var values = lines?.ToArray() ?? throw new DomainException("Adjustment lines are required.");
        if (values.Length == 0 || values.Any(line => line is null))
        {
            throw new DomainException("Adjustment must contain at least one valid line.");
        }
        if (values.Select(line => (line.WarehouseId, line.ProductItemId)).Distinct().Count() != values.Length)
        {
            throw new DomainException("Adjustment lines must be unique by warehouse and product item.");
        }
        return values;
    }
}
