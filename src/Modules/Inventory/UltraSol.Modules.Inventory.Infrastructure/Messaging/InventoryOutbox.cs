using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Modules.Inventory.Infrastructure.Persistence;
using UltraSol.Shared.IntegrationEvents.Inventory;
using UltraSol.Shared.Infrastructure.Messaging;

namespace UltraSol.Modules.Inventory.Infrastructure.Messaging;

public sealed class InventoryOutbox(Mailbox<InventoryDbContext> mailbox) : IInventoryOutbox
{
    public void Add(InventoryReservationExpiredV1 message) => mailbox.Add(message);
}