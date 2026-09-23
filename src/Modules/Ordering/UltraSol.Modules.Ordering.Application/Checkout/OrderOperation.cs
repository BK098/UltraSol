namespace UltraSol.Modules.Ordering.Application.Checkout;

public sealed class OrderOperation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OrderId { get; set; }
    public string Kind { get; set; } = "Cancel";
    public string? Reason { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? FulfillmentId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string State { get; set; } = "Pending";
    public string? ErrorCode { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string Version { get; set; } = Guid.NewGuid().ToString("N");
}