namespace UltraSol.Shared.IntegrationEvents.Auth;

public sealed record EmployeeAccessApplied(Guid EventId, Guid CorrelationId, long AggregateVersion, int ContractVersion, Guid EmployeeId, bool IsActive, string? Error);