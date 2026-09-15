namespace UltraSol.Modules.Inventory.Domain;

public static class InventoryTime
{
    // PostgreSQL timestamps store microseconds; canonicalize before comparing retry payloads.
    public static DateTimeOffset ToUtc(DateTimeOffset value) => new(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);
}
