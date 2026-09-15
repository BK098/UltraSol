using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Shared.Application.Messaging.Integration;
using UltraSol.Shared.IntegrationEvents.Auth;
using UltraSol.Shared.IntegrationEvents.Organization;

namespace UltraSol.Modules.Auth.Application.Messaging;

public sealed class EmployeeAccountRequestedHandler(IAuthRepository repository, IAuthOutbox outbox, UserManager<UserAccount> users) : IIntegrationHandler<EmployeeAccountRequested>
{
    public async Task HandleAsync(EmployeeAccountRequested message, CancellationToken ct)
    {
        await repository.LockAsync(ct);
        var existing = await repository.Employees.SingleOrDefaultAsync(value => value.EmployeeId == message.EmployeeId, ct);
        if (existing is not null)
        {
            return;
        }
        Guid? userId = null;
        string? error = null;
        if (await users.FindByEmailAsync(message.Email.Trim()) is not null)
        {
            error = "Email already belongs to an account.";
        }
        else
        {
            var account = UserAccount.Create(message.Email.Trim(), DateTimeOffset.UtcNow);
            // Explicit employee-provisioning exception; registration and password changes retain normal validation.
            account.PasswordHash = users.PasswordHasher.HashPassword(account, "abc@123");
            var result = await users.CreateAsync(account);
            if (result.Succeeded)
            {
                userId = account.Id;
                repository.Add(new EmployeeAccount { EmployeeId = message.EmployeeId, UserId = account.Id, Version = message.AggregateVersion });
            }
            else
            {
                error = string.Join("; ", result.Errors.Select(value => value.Code));
            }
        }
        outbox.Add(new EmployeeAccountProvisioned(Guid.NewGuid(), message.CorrelationId, message.AggregateVersion, 1, message.EmployeeId, userId, error));
    }
}