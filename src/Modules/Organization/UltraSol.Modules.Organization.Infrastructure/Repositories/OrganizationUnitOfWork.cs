using UltraSol.Modules.Organization.Domain.Repositories;
using UltraSol.Modules.Organization.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Infrastructure.Repositories;
namespace UltraSol.Modules.Organization.Infrastructure.Repositories;

public sealed class OrganizationUnitOfWork(OrganizationDbContext context, IDomainEventDispatcher dispatcher) : UnitOfWork(context, dispatcher), IOrganizationUnitOfWork;