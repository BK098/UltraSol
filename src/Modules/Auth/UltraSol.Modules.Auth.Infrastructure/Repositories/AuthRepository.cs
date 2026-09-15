using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Modules.Auth.Domain.Authorization;
using UltraSol.Modules.Auth.Domain.Sessions;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Infrastructure.Persistence;
namespace UltraSol.Modules.Auth.Infrastructure.Repositories;
public sealed class AuthRepository(AuthDbContext db) : IAuthRepository
{
    public IQueryable<UserAccount> Users => db.Set<UserAccount>();
    public IQueryable<Session> Sessions => db.Set<Session>();
    public IQueryable<Role> Roles => db.Set<Role>();
    public IQueryable<PermissionDefinition> Permissions => db.Set<PermissionDefinition>();
    public IQueryable<RolePermission> RolePermissions => db.Set<RolePermission>();
    public IQueryable<RoleAssignment> RoleAssignments => db.Set<RoleAssignment>();
    public IQueryable<DirectPermission> DirectPermissions => db.Set<DirectPermission>();
    public IQueryable<RefreshToken> RefreshTokens => db.Set<RefreshToken>();
    public IQueryable<EmployeeAccount> Employees => db.Set<EmployeeAccount>();
    public void Add<T>(T entity)
        where T : class => db.Set<T>().Add(entity);
    public void Remove<T>(T entity)
        where T : class => db.Set<T>().Remove(entity);
    public void AddRange<T>(IEnumerable<T> entities)
        where T : class => db.Set<T>().AddRange(entities);
    public void RemoveRange<T>(IEnumerable<T> entities)
        where T : class => db.Set<T>().RemoveRange(entities);
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
    public Task LockAsync(CancellationToken ct) => db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(731010, 1)", ct);
}