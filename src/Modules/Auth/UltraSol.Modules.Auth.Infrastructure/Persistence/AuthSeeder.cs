using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Modules.Auth.Domain.Authorization;
using UltraSol.Modules.Auth.Infrastructure.Authorization;


namespace UltraSol.Modules.Auth.Infrastructure.Persistence;

public sealed class AuthSeeder(AuthDbContext db, IAuthUnitOfWork unit, UserManager<UserAccount> users, PermissionCatalogSynchronizer permissions)
{
    public async Task SeedAsync(string path, CancellationToken ct)
    {
        await permissions.SynchronizeAsync(ct);
        var seed = JsonSerializer.Deserialize<Seed>(await File.ReadAllTextAsync(path, ct)) ?? throw new InvalidOperationException("Invalid Auth seed.");
        await unit.ExecuteInTransactionAsync(async transactionCt =>
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(731010, 1)", transactionCt);
            var roles = await db.Roles.ToDictionaryAsync(value => value.Code, transactionCt);
            foreach (var configured in seed.Roles)
            {
                if (!roles.ContainsKey(configured.Code))
                {
                    var role = new Role(configured.Code, configured.Name);
                    roles.Add(role.Code, role);
                    db.Roles.Add(role);
                }
            }
            var system = roles["System"];
            var exists = await (from assignment in db.RoleAssignments join user in db.Users on assignment.UserId equals user.Id
                                where assignment.RoleId == system.Id && user.Status == AccountStatus.Active
                                select user.Id).AnyAsync(transactionCt);
            if (!exists)
            {
                var normalized = users.NormalizeEmail(seed.SystemAccount.Email.Trim());
                if (await db.Users.IgnoreQueryFilters().AnyAsync(value => value.NormalizedEmail == normalized, transactionCt))
                {
                    throw new InvalidOperationException("Seed email already belongs to an account. Refusing automatic privilege escalation.");
                }
                var account = UserAccount.Create(seed.SystemAccount.Email, DateTimeOffset.UtcNow);
                // Explicit bootstrap-only password exception. Normal registration and password changes still validate passwords.
                account.PasswordHash = users.PasswordHasher.HashPassword(account, seed.SystemAccount.Password);
                var result = await users.CreateAsync(account);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException("Unable to seed System: " + string.Join("; ", result.Errors.Select(value => value.Description)));
                }
                db.RoleAssignments.Add(new RoleAssignment(Guid.CreateVersion7(), account.Id, system.Id));
            }
        }, ct);
    }

    private sealed record Seed(RoleSeed[] Roles, AccountSeed SystemAccount);
    private sealed record RoleSeed(string Code, string Name);
    private sealed record AccountSeed(string Email, string Password);
}