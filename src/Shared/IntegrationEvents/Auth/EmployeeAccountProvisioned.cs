namespace UltraSol.Shared.IntegrationEvents.Auth;

public sealed record EmployeeAccountProvisioned(Guid EventId, Guid CorrelationId, long AggregateVersion, int ContractVersion, Guid EmployeeId, Guid? UserId, string? Error);