using UltraSol.Shared.Infrastructure.Messaging;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Infrastructure.Persistence;
namespace UltraSol.Modules.Auth.Infrastructure.Messaging;
public sealed class AuthOutbox(Mailbox<AuthDbContext> mailbox) : IAuthOutbox
{
    public void Add<T>(T message) => mailbox.Add(message);
}