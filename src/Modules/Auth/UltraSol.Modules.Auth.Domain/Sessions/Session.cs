using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Events;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Auth.Domain.Sessions;

public sealed class Session : AggregateRoot
{
    private Session() { }

    public Guid UserId { get; private set; }
    public Device Device { get; private set; } = null!;
    public DateTimeOffset LastSeenAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevokeReason { get; private set; }

    public static Session Create(Guid userId, Device device, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        Guard.Id(userId);
        ArgumentNullException.ThrowIfNull(device);
        Guard.AgainstDefault(now);
        if (expiresAt <= now)
        {
            throw new DomainException("Session expiry must be after creation.");
        }
        return new Session
        {
            UserId = userId,
            Device = device,
            CreatedAt = now.ToUniversalTime(),
            LastSeenAt = now.ToUniversalTime(),
            ExpiresAt = expiresAt.ToUniversalTime()
        };
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now >= CreatedAt && now < ExpiresAt;

    public void Touch(DateTimeOffset now)
    {
        if (!IsActive(now) || now < LastSeenAt)
        {
            throw new DomainException("Only active sessions can advance their last-seen time.");
        }
        if (now == LastSeenAt)
        {
            return;
        }
        LastSeenAt = now.ToUniversalTime();
        MarkUpdated(null, now.ToUniversalTime());
    }

    public void Revoke(string reason, DateTimeOffset now)
    {
        if (RevokedAt is not null)
        {
            return;
        }
        reason = Guard.Required(reason, "Revoke reason");
        if (reason.Length > 256 || now < LastSeenAt)
        {
            throw new DomainException("Revoke reason is too long or revoke time precedes session activity.");
        }
        RevokedAt = now.ToUniversalTime();
        RevokeReason = reason;
        MarkUpdated(null, now.ToUniversalTime());
        AddDomainEvent(new SessionRevoked(Id, UserId, reason) { OccurredAt = now.ToUniversalTime() });
    }

    public override void MarkDeleted(string? actorId, DateTimeOffset utcNow) =>
        throw new DomainException("Use Revoke to end a session.");

    public override void Restore() => throw new DomainException("Sessions cannot be restored.");
}

public sealed record SessionRevoked(Guid SessionId, Guid UserId, string Reason) : DomainEvent;