namespace UltraSol.Shared.IntegrationEvents.Organization;

public sealed record EmployeeAccountRequested(Guid EventId, Guid CorrelationId, long AggregateVersion, int ContractVersion, Guid EmployeeId, string Email, Guid DepartmentId);