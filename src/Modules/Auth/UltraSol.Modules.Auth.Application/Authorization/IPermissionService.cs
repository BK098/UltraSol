namespace UltraSol.Modules.Auth.Application.Authorization;

public interface IPermissionService
{
    Task<PermissionSnapshot> GetAsync(Guid userId, CancellationToken ct);
    Task DemandAsync(string permission, CancellationToken ct);
}