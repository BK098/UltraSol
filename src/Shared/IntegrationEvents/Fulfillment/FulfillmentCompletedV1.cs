namespace UltraSol.Shared.IntegrationEvents.Fulfillment;

public sealed record FulfillmentCompletedV1(Guid EventId, Guid CorrelationId, int ContractVersion, DateTimeOffset OccurredAt,
    Guid OrderId, Guid ReservationId, Guid FulfillmentId, DateTimeOffset CompletedAt);