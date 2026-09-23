namespace UltraSol.Shared.IntegrationEvents.Inventory;

public sealed record InventoryReservationExpiredV1(Guid EventId, Guid CorrelationId, int ContractVersion, DateTimeOffset OccurredAt,
    Guid ReservationId, Guid OrderId, DateTimeOffset ExpiredAt);