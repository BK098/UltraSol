using UltraSol.Modules.Organization.Domain.Employees;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Organization.Domain.Departments;

public sealed class Department : AggregateRoot
{
    private Department() { }
    public Department(string name, Guid? parentId)
    {
        Name = Guard.Required(name, "Department name");
        ParentId = parentId;
        Version = 1;
    }
    public string Name { get; private set; } = null!;
    public Guid? ParentId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public long Version { get; private set; }
    public IList<Employee> Employees { get; private set; } = [];
    public IList<Department> Children { get; private set; } = [];
    public void Update(string name, Guid? parentId, bool active)
    {
        Name = Guard.Required(name, "Department name");
        ParentId = parentId;
        IsActive = active;
        Version++;
    }
}