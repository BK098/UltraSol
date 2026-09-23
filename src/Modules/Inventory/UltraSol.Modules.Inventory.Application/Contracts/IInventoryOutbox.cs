using UltraSol.Shared.IntegrationEvents.Inventory;

namespace UltraSol.Modules.Inventory.Application.Contracts;

public interface IInventoryOutbox
{
    void Add(InventoryReservationExpiredV1 message);
}