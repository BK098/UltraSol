using UltraSol.Shared.Domain.Common.Entities;

namespace UltraSol.Modules.Ordering.Domain.Orders;

public sealed class OrderHistory : BaseEntity
{
    private OrderHistory() { }
    internal OrderHistory(OrderStatus status, long version, string? reason, DateTimeOffset at)
    {
        Id = Guid.CreateVersion7();
        Status = status;
        Version = version;
        Reason = reason;
        OccurredAt = at;
    }
    public OrderStatus Status { get; private set; }
    public long Version { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}

public sealed class OrderNote : BaseEntity
{
    private OrderNote() { }
    internal OrderNote(string text, Guid? actorId, DateTimeOffset at)
    {
        Id = Guid.CreateVersion7();
        Text = text;
        ActorId = actorId;
        CreatedAt = at;
    }
    public string Text { get; private set; } = "";
    public Guid? ActorId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}