using UltraSol.Modules.Organization.Domain.Departments;
using UltraSol.Shared.Domain.Common.Repositories;
namespace UltraSol.Modules.Organization.Domain.Repositories;
public interface IDepartmentRepository : IRepository<Department>
{
    Task<IReadOnlyList<Department>> GetOrganizationTreeAsync(Guid departmentId,CancellationToken ct);
}