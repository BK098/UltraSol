namespace UltraSol.Modules.Ordering.Application.Checkout;

public enum CheckoutStage { Preparing, Quoted, Reserved, Completed, Compensating, Failed }

public sealed class CheckoutAttempt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string OwnerKey { get; set; } = "";
    public Guid? IdentityUserId { get; set; }
    public string? GuestTokenHash { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public Guid OrderId { get; set; } = Guid.CreateVersion7();
    public Guid? CartId { get; set; }
    public Guid? NegotiationTransactionRef { get; set; }
    public CheckoutInput Input { get; set; } = null!;
    public CatalogItem[]? CatalogItems { get; set; }
    public PriceQuote? Quote { get; set; }
    public StockLine[]? StockLines { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public Guid? ReservationId { get; set; }
    public CheckoutStage Stage { get; set; }
    public bool ReserveStarted { get; set; }
    public bool ExpiryObserved { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ErrorStatus { get; set; }
    public CheckoutResult? Result { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string Version { get; set; } = Guid.NewGuid().ToString("N");
}