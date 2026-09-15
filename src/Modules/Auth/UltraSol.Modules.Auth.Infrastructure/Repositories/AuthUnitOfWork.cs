using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Auth.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Auth.Infrastructure.Repositories;

public sealed class AuthUnitOfWork(AuthDbContext context, IDomainEventDispatcher dispatcher) : UnitOfWork(context, dispatcher), IAuthUnitOfWork;