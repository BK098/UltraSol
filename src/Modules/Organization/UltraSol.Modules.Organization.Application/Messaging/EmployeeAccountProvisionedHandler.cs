using UltraSol.Shared.Application.Messaging.Integration;
using UltraSol.Shared.IntegrationEvents.Auth;
using UltraSol.Modules.Organization.Domain.Repositories;
namespace UltraSol.Modules.Organization.Application.Messaging;
public sealed class EmployeeAccountProvisionedHandler(IEmployeeRepository employees) : IIntegrationHandler<EmployeeAccountProvisioned>
{
    public async Task HandleAsync(EmployeeAccountProvisioned message, CancellationToken ct)
    {
        var employee = await employees.GetTrackedRequiredAsync(message.EmployeeId, ct);
        employee.Complete(message.CorrelationId, message.AggregateVersion, message.UserId, message.Error);
    }
}