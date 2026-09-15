using UltraSol.Shared.Application.Messaging.Integration;
using UltraSol.Shared.IntegrationEvents.Auth;
using UltraSol.Modules.Organization.Domain.Repositories;
namespace UltraSol.Modules.Organization.Application.Messaging;
public sealed class EmployeeAccessAppliedHandler(IEmployeeRepository employees) : IIntegrationHandler<EmployeeAccessApplied>
{
    public async Task HandleAsync(EmployeeAccessApplied message, CancellationToken ct)
    {
        var employee = await employees.GetTrackedRequiredAsync(message.EmployeeId, ct);
        employee.Complete(message.CorrelationId, message.AggregateVersion, employee.UserId, message.Error);
    }
}