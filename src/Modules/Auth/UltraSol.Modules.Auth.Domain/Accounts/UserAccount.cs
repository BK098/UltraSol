using Microsoft.AspNetCore.Identity;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Events;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Auth.Domain.Accounts;

public sealed class UserAccount : IdentityUser<Guid>, IAggregateRoot, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private UserAccount() { }

    public AccountStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // UserManager performs email/password validation and normalization before persistence.
    public static UserAccount Create(string email, DateTimeOffset now)
    {
        Guard.AgainstDefault(now);
        var address = Guard.Required(email, "Email");
        return new UserAccount
        {
            Id = Guid.CreateVersion7(),
            Email = address,
            UserName = address,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = now.ToUniversalTime(),
            Status = AccountStatus.Active
        };
    }

    public void Suspend(DateTimeOffset now) => ChangeStatus(AccountStatus.Suspended, now);

    public void Activate(DateTimeOffset now) => ChangeStatus(AccountStatus.Active, now);

    public void SoftDelete(DateTimeOffset now) => ChangeStatus(AccountStatus.Deleted, now);

    private void ChangeStatus(AccountStatus status, DateTimeOffset now)
    {
        if (Status == status)
        {
            return;
        }
        if (Status == AccountStatus.Deleted)
        {
            throw new DomainException("Deleted accounts cannot be restored. Register a new account.");
        }
        Guard.AgainstDefault(now);
        if (now < CreatedAt)
        {
            throw new DomainException("Account changes cannot precede account creation.");
        }
        Status = status;
        if (status == AccountStatus.Deleted)
        {
            DeletedAt = now.ToUniversalTime();
        }
        // Existing authentication tickets must not become valid again after reactivation.
        SecurityStamp = Guid.NewGuid().ToString("N");
        ConcurrencyStamp = Guid.NewGuid().ToString("N");
        AddDomainEvent(new AccountStatusChanged(Id, status) { OccurredAt = now.ToUniversalTime() });
    }

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    public void RemoveDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Remove(domainEvent);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}

public sealed record AccountStatusChanged(Guid UserId, AccountStatus Status) : DomainEvent;