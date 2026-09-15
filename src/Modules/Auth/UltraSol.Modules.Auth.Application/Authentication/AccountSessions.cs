using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Modules.Auth.Domain.Sessions;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Auth.Application.Authentication;

public sealed class AccountSessions(IAuthRepository db, ICurrentAccount current, IAuthTokens tokens)
{
    public LoginResult Issue(UserAccount user, Session session, string mode)
    {
        string? access = null;
        if (mode == "token")
        {
            access = tokens.Access(user.Id, session.Id, user.Email!);
        }
        return new LoginResult(user.Id, session.Id, user.Email!, mode, access, null, session.ExpiresAt);
    }

    public Task<UserAccount> CurrentAsync(CancellationToken ct) => db.Users.SingleAsync(value => value.Id == current.UserId, ct);

    public async Task RevokeAsync(Guid userId, Guid? sessionId, bool keepCurrent, string reason, CancellationToken ct)
    {
        var sessions = await db.Sessions.Where(value => value.UserId == userId && value.RevokedAt == null &&
            (!sessionId.HasValue || value.Id == sessionId) && (!keepCurrent || value.Id != current.SessionId)).ToListAsync(ct);
        foreach (var session in sessions)
        {
            session.Revoke(reason, DateTimeOffset.UtcNow);
        }
    }

    public static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new DomainException(string.Join("; ", result.Errors.Select(value => value.Description)));
        }
    }
}