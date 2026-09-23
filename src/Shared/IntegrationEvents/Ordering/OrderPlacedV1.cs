namespace UltraSol.Shared.IntegrationEvents.Ordering;

public sealed record OrderPlacedV1(Guid EventId, Guid CorrelationId, int ContractVersion, DateTimeOffset OccurredAt,
    Guid OrderId, long OrderVersion, string OrderNumber, string BuyerType, Guid? IdentityUserId, Guid? CustomerId,
    Guid? BusinessAccountId, string BuyerName, string BuyerEmail, string BuyerPhone, decimal GrandTotal, string Currency,
    string PaymentTerm, int? NetDays, DateTimeOffset ReservationExpiresAt, string OrderStatus = "Placed");
