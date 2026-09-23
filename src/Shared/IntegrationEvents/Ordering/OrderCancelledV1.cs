namespace UltraSol.Shared.IntegrationEvents.Ordering;

public sealed record OrderCancelledV1(Guid EventId, Guid CorrelationId, int ContractVersion, DateTimeOffset OccurredAt,
    Guid OrderId, long OrderVersion, string Reason);