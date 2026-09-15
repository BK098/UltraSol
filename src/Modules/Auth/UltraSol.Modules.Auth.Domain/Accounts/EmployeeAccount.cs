namespace UltraSol.Modules.Auth.Domain.Accounts;
public sealed class EmployeeAccount
{
    public Guid EmployeeId { get; set; }
    public Guid UserId { get; set; }
    public long Version { get; set; }
    public bool IsActive { get; set; } = true;
}