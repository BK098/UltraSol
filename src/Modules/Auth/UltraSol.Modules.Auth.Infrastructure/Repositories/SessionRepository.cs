using UltraSol.Modules.Auth.Domain.Sessions;
using UltraSol.Modules.Auth.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Auth.Infrastructure.Repositories;

public sealed class SessionRepository(AuthDbContext context) : Repository<Session>(context);