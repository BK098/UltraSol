using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Auth.Domain.Authorization;

public sealed class Role
{
    private Role() { }
    public Role(string code, string name)
    {
        Code = Guard.Required(code, "Role code");
        Name = Guard.Required(name, "Role name");
    }
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public void Rename(string name) => Name = Guard.Required(name, "Role name");
}

public sealed record PermissionDefinition(string Code, string RequestType)
{
    public bool IsActive { get; set; } = true;
}

public sealed record RolePermission(Guid RoleId, string PermissionCode, bool Deny);
public sealed record RoleAssignment(Guid Id, Guid UserId, Guid RoleId);
public sealed record DirectPermission(Guid Id, Guid UserId, string PermissionCode, bool Deny);
public sealed class AuthorizationVersion
{
    public int Id { get; set; } = 1;
    public long Version { get; set; } = 1;
}