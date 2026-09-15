namespace UltraSol.Modules.Auth.Domain.Sessions;

public sealed class RefreshToken
{
    private RefreshToken() { }
    public RefreshToken(Guid sessionId, string hash, DateTimeOffset expiresAt)
    {
        SessionId = sessionId;
        Hash = hash;
        ExpiresAt = expiresAt;
    }
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid SessionId { get; private set; }
    public string Hash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public void Consume(DateTimeOffset now) => ConsumedAt = now;
}