using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Organization.Domain.Employees;

public sealed class Employee : AggregateRoot
{
    private Employee() { }
    public Employee(string email, Guid departmentId)
    {
        Email = Guard.Required(email, "Employee email").Trim();
        DepartmentId = Guard.Id(departmentId);
    }
    public Guid? UserId { get; private set; }
    public string Email { get; private set; } = null!;
    public Guid DepartmentId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool PreviousIsActive { get; private set; } = true;
    public long Version { get; private set; } = 1;
    public Guid CorrelationId { get; private set; } = Guid.NewGuid();
    public string SyncStatus { get; private set; } = "Pending";
    public string? SyncError { get; private set; }
    public void Update(Guid departmentId, bool active)
    {
        if (SyncStatus == "Pending" || UserId is null)
        {
            throw new DomainException("Employee account synchronization must complete first.");
        }
        DepartmentId = Guard.Id(departmentId);
        PreviousIsActive = IsActive;
        IsActive = active;
        Version++;
        CorrelationId = Guid.NewGuid();
        SyncStatus = "Pending";
        SyncError = null;
    }
    public void Complete(Guid correlationId, long version, Guid? userId, string? error)
    {
        if (correlationId != CorrelationId || version != Version || SyncStatus != "Pending")
        {
            return;
        }
        SyncError = error;
        SyncStatus = error is null ? "Completed" : "Rejected";
        if (error is null)
        {
            UserId = userId ?? UserId;
        }
        else
        {
            IsActive = UserId.HasValue && PreviousIsActive;
        }
    }
}