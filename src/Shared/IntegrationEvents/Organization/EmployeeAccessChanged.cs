namespace UltraSol.Shared.IntegrationEvents.Organization;

public sealed record EmployeeAccessChanged(Guid EventId, Guid CorrelationId, long AggregateVersion, int ContractVersion, Guid EmployeeId, Guid UserId, bool IsActive);