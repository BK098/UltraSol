using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Inventory.Domain.Reservations;

public enum ReservationStatus
{
    Active,
    Released,
    Consumed,
    Expired
}

public sealed class StockReservation : AggregateRoot
{
    private readonly List<ReservationLine> _lines = [];

    private StockReservation() { }

    public StockReservation(Guid orderId, DateTimeOffset expiresAt, IEnumerable<ReservationLine> lines, DateTimeOffset now)
    {
        OrderId = Guard.Id(orderId);
        var normalizedExpiry = InventoryTime.ToUtc(expiresAt);
        if (normalizedExpiry <= now.ToUniversalTime())
        {
            throw new DomainException("Reservation expiry must be in the future.");
        }
        ExpiresAt = normalizedExpiry;
        _lines.AddRange(ValidateLines(lines));
        Status = ReservationStatus.Active;
    }

    public Guid OrderId { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? ExpiredAt { get; private set; }
    public IReadOnlyCollection<ReservationLine> Lines => _lines.AsReadOnly();

    public bool Matches(DateTimeOffset expiresAt, IEnumerable<ReservationLine> lines)
    {
        var values = lines?.ToArray();
        if (values is null || values.Length != _lines.Count)
        {
            return false;
        }
        return InventoryTime.ToUtc(expiresAt) == ExpiresAt &&
            BusinessLines(values).SequenceEqual(BusinessLines(_lines));
    }

    public ReservationStatus EffectiveStatus(DateTimeOffset now)
    {
        return Status == ReservationStatus.Active && IsDue(now) ? ReservationStatus.Expired : Status;
    }

    public bool Release(DateTimeOffset now)
    {
        if (Status == ReservationStatus.Consumed)
        {
            throw new DomainException("Consumed reservations cannot be released.");
        }
        if (Status is ReservationStatus.Released or ReservationStatus.Expired || IsDue(now))
        {
            return false;
        }
        Status = ReservationStatus.Released;
        ReleasedAt = now.ToUniversalTime();
        return true;
    }

    public bool Consume(DateTimeOffset now)
    {
        if (Status == ReservationStatus.Consumed)
        {
            return false;
        }
        if (Status != ReservationStatus.Active || IsDue(now))
        {
            throw new DomainException("Only an active reservation before expiry can be consumed.");
        }
        Status = ReservationStatus.Consumed;
        ConsumedAt = now.ToUniversalTime();
        return true;
    }

    public bool Expire(DateTimeOffset now)
    {
        if (Status != ReservationStatus.Active || !IsDue(now))
        {
            return false;
        }
        Status = ReservationStatus.Expired;
        ExpiredAt = now.ToUniversalTime();
        return true;
    }

    public override void MarkDeleted(string? actorId, DateTimeOffset utcNow)
    {
        throw new DomainException("Stock reservations cannot be deleted.");
    }

    public override void Restore()
    {
        throw new DomainException("Stock reservations cannot be restored.");
    }

    private bool IsDue(DateTimeOffset now) => now.ToUniversalTime() >= ExpiresAt;

    private static ReservationLine[] ValidateLines(IEnumerable<ReservationLine> lines)
    {
        var values = lines?.ToArray() ?? throw new DomainException("Reservation lines are required.");
        if (values.Length == 0 || values.Any(line => line is null))
        {
            throw new DomainException("Reservation must contain at least one valid line.");
        }
        if (values.Select(line => (line.WarehouseId, line.ProductItemId)).Distinct().Count() != values.Length)
        {
            throw new DomainException("Reservation lines must be unique by warehouse and product item.");
        }
        return values;
    }

    private static IEnumerable<(Guid WarehouseId, Guid ProductItemId, int Quantity)> BusinessLines(
        IEnumerable<ReservationLine> lines) =>
        lines.Select(line => (line.WarehouseId, line.ProductItemId, line.Quantity))
            .OrderBy(line => line.WarehouseId)
            .ThenBy(line => line.ProductItemId)
            .ThenBy(line => line.Quantity);
}
