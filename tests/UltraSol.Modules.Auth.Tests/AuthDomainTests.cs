using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Modules.Auth.Domain.Sessions;
using UltraSol.Shared.Domain.Common.Exceptions;
using Xunit;

namespace UltraSol.Modules.Auth.Tests;

public sealed class AuthDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AccountLifecycleKeepsDeletionTerminalAndRegistrationIndependent()
    {
        var account = UserAccount.Create(" user@example.com ", Now);
        var stamp = account.SecurityStamp;
        Assert.Equal("user@example.com", account.Email);
        Assert.Equal(account.Email, account.UserName);
        account.Suspend(Now);
        Assert.Equal(AccountStatus.Suspended, account.Status);
        Assert.NotEqual(stamp, account.SecurityStamp);
        account.Activate(Now);
        account.SoftDelete(Now.AddMinutes(1));
        account.SoftDelete(Now.AddMinutes(2));
        Assert.Equal(Now.AddMinutes(1), account.DeletedAt);
        Assert.Throws<DomainException>(() => account.Activate(Now));
        Assert.Throws<DomainException>(() => account.Suspend(Now));
        Assert.Equal(3, account.DomainEvents.Count);
        var replacement = UserAccount.Create(account.Email!, Now.AddMinutes(3));
        Assert.NotEqual(account.Id, replacement.Id);
        Assert.NotEqual(account.SecurityStamp, replacement.SecurityStamp);
        Assert.Equal(AccountStatus.Active, replacement.Status);
        Assert.Null(replacement.PasswordHash);
    }

    [Fact]
    public void SessionExpiryIsExclusiveAndTouchCannotExtendOrResurrectIt()
    {
        var session = Session.Create(Guid.NewGuid(), Device.Create("Browser"), Now, Now.AddHours(1));
        Assert.False(session.IsActive(Now.AddTicks(-1)));
        Assert.True(session.IsActive(Now));
        var stamp = session.ConcurrencyStamp;
        session.Touch(Now.AddMinutes(1));
        Assert.NotEqual(stamp, session.ConcurrencyStamp);
        Assert.Equal(Now.AddHours(1), session.ExpiresAt);
        Assert.Throws<DomainException>(() => session.Touch(Now));
        Assert.False(session.IsActive(session.ExpiresAt));
        Assert.Throws<DomainException>(() => session.Touch(session.ExpiresAt));
        Assert.Throws<DomainException>(() => session.Restore());
        Assert.Throws<DomainException>(() => session.MarkDeleted(null, Now));
    }

    [Fact]
    public void RevokeIsIdempotentAndPreservesTheFirstReasonAndTime()
    {
        var session = Session.Create(Guid.NewGuid(), Device.Create("Browser"), Now, Now.AddHours(1));
        session.Revoke(" Logout ", Now.AddMinutes(1));
        session.Revoke("Other reason", Now.AddMinutes(2));
        Assert.Equal("Logout", session.RevokeReason);
        Assert.Equal(Now.AddMinutes(1), session.RevokedAt);
        Assert.Single(session.DomainEvents);
        Assert.False(session.IsActive(Now.AddMinutes(2)));
        Assert.Throws<DomainException>(() => session.Touch(Now.AddMinutes(2)));
    }

    [Fact]
    public void InvalidAccountSessionAndDeviceInputsAreRejected()
    {
        Assert.Throws<DomainException>(() => UserAccount.Create(" ", Now));
        Assert.Throws<DomainException>(() => UserAccount.Create("a@example.com", Now).Suspend(Now.AddTicks(-1)));
        Assert.Throws<DomainException>(() => Session.Create(Guid.Empty, Device.Create("Browser"), Now, Now.AddHours(1)));
        Assert.Throws<DomainException>(() => Session.Create(Guid.NewGuid(), Device.Create("Browser"), Now, Now));
        Assert.Throws<DomainException>(() => Device.Create(" "));
        Assert.Throws<DomainException>(() => Device.Create(new string('a', 129)));
        Assert.Throws<DomainException>(() => Device.Create("Browser", new string('a', 1025)));
        Assert.Throws<DomainException>(() => Device.Create("Browser", ipAddress: "not-an-ip"));
        var session = Session.Create(Guid.NewGuid(), Device.Create("Browser"), Now, Now.AddHours(1));
        Assert.Throws<DomainException>(() => session.Revoke(" ", Now));
        Assert.Throws<DomainException>(() => session.Revoke("Logout", Now.AddTicks(-1)));
        Assert.Throws<DomainException>(() => session.Revoke(new string('a', 257), Now));
    }

    [Fact]
    public void DeviceEqualityUsesNormalizedValues()
    {
        var first = Device.Create(" Browser ", " Agent ", "0:0:0:0:0:0:0:1");
        var second = Device.Create("Browser", "Agent", "::1");
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }
}