using UltraSol.Modules.Organization.Domain;
using UltraSol.Modules.Organization.Domain.Employees;
using UltraSol.Modules.Organization.Domain.Repositories;
using UltraSol.Modules.Organization.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Repositories;
namespace UltraSol.Modules.Organization.Infrastructure.Repositories;

public sealed class EmployeeRepository(OrganizationDbContext context) : Repository<Employee>(context), IEmployeeRepository;