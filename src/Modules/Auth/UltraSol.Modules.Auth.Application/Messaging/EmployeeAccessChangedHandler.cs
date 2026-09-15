using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Shared.Application.Messaging.Integration;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.IntegrationEvents.Auth;
using UltraSol.Shared.IntegrationEvents.Organization;

namespace UltraSol.Modules.Auth.Application.Messaging;

public sealed class EmployeeAccessChangedHandler(IAuthRepository repository, IAuthOutbox outbox, AuthorizationRules rules) : IIntegrationHandler<EmployeeAccessChanged>
{
    public async Task HandleAsync(EmployeeAccessChanged message, CancellationToken ct)
    {
        await repository.LockAsync(ct);
        var employee = await repository.Employees.SingleAsync(value => value.EmployeeId == message.EmployeeId && value.UserId == message.UserId, ct);
        if (message.AggregateVersion <= employee.Version)
        {
            return;
        }
        string? error = null;
        if (!message.IsActive)
        {
            try
            {
                await rules.ProtectLastSystemAsync(employee.UserId, null, ct);
            }
            catch (DomainException exception)
            {
                error = exception.Message;
            }
        }
        employee.Version = message.AggregateVersion;
        if (error is null)
        {
            employee.IsActive = message.IsActive;
            if (!message.IsActive)
            {
                var sessions = await repository.Sessions.Where(value => value.UserId == message.UserId && value.RevokedAt == null).ToListAsync(ct);
                foreach (var session in sessions)
                {
                    session.Revoke("Employee disabled", DateTimeOffset.UtcNow);
                }
            }
        }
        outbox.Add(new EmployeeAccessApplied(Guid.NewGuid(), message.CorrelationId, message.AggregateVersion, 1, message.EmployeeId, employee.IsActive, error));
    }
}