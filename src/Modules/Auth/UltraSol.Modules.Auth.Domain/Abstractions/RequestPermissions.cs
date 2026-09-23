namespace UltraSol.Modules.Auth.Domain.Abstractions;

public sealed record PermissionRule(string Code, bool Deny);
public sealed record PermissionSnapshot(bool IsSystem, string[] Roles, PermissionRule[] Rules)
{
    public long Version { get; init; }
    public bool Allows(string permission)
    {
        if (IsSystem)
        {
            return true;
        }
        var matching = Rules.Where(rule => rule.Code == permission).ToArray();
        return matching.Any(rule => !rule.Deny) && !matching.Any(rule => rule.Deny);
    }
}

public sealed class AuthAccessException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}