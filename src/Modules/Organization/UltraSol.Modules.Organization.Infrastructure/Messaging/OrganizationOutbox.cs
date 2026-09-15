using UltraSol.Modules.Organization.Application.Messaging;
using UltraSol.Modules.Organization.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Messaging;

namespace UltraSol.Modules.Organization.Infrastructure.Messaging;

public sealed class OrganizationOutbox(Mailbox<OrganizationDbContext> mailbox) : IOrganizationOutbox
{
    public void Add<T>(T message) => mailbox.Add(message);
}