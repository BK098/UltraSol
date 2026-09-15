using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Modules.Auth.Domain.Authorization;
using UltraSol.Modules.Auth.Domain.Sessions;

namespace UltraSol.Modules.Auth.Application.Persistence;
public interface IAuthRepository
{
    IQueryable<UserAccount> Users { get; }
    IQueryable<Session> Sessions { get; }
    IQueryable<Role> Roles { get; }
    IQueryable<PermissionDefinition> Permissions { get; }
    IQueryable<RolePermission> RolePermissions { get; }
    IQueryable<RoleAssignment> RoleAssignments { get; }
    IQueryable<DirectPermission> DirectPermissions { get; }
    IQueryable<RefreshToken> RefreshTokens { get; }
    IQueryable<EmployeeAccount> Employees { get; }
    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    void AddRange<T>(IEnumerable<T> entities) where T : class;
    void RemoveRange<T>(IEnumerable<T> entities) where T : class;
    Task SaveAsync(CancellationToken ct);
    Task LockAsync(CancellationToken ct);
}