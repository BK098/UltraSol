namespace UltraSol.Shared.IntegrationEvents.Ordering;

public sealed record OrderCompletedV1(Guid EventId, Guid CorrelationId, int ContractVersion, DateTimeOffset OccurredAt,
    Guid OrderId, long OrderVersion, string BuyerType, Guid? IdentityUserId, Guid? CustomerId, Guid? BusinessAccountId,
    string BuyerName, string BuyerEmail, string BuyerPhone);