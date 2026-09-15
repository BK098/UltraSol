using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Auth.Application.Authentication;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Modules.Auth.Domain.Sessions;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using static UltraSol.Modules.Auth.Application.Authentication.AccountSessions;

namespace UltraSol.Modules.Auth.Application.Features.Accounts.Commands;

public sealed record LoginCommand(string Email, string Password, string Mode = "cookie", string DeviceName = "Browser") : ICommand<ApiResult<object>>, IAnonymousAuthRequest;
public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.Mode).NotEmpty().MaximumLength(256);
        RuleFor(x => x.DeviceName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Mode).Must(mode => mode is "cookie" or "token");
    }
}
internal sealed class LoginCommandHandler(IAuthRepository db, UserManager<UserAccount> users, IAuthTokens tokens, AccountSessions accounts, IAuthUnitOfWork unitOfWork) : ICommandHandler<LoginCommand, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var result = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await db.LockAsync(ct);
            return await ExecuteAsync(request, ct);
        }, cancellationToken);
        if (result is Rejected rejected)
        {
            throw new AuthAccessException(401, rejected.Message);
        }
        if (result is LoginResult login && login.Mode == "cookie")
        {
            await tokens.SignInAsync(login);
        }
        return ApiResultBuilder.Success<object>(result);
    }
    private async Task<object> ExecuteAsync(LoginCommand request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (request.Mode is not ("cookie" or "token"))
        {
            throw new DomainException("Mode must be cookie or token.");
        }
        var user = await users.FindByEmailAsync(request.Email?.Trim() ?? "");
        if (user is null || user.Status != AccountStatus.Active || await users.IsLockedOutAsync(user))
        {
            return new Rejected("Invalid credentials.");
        }
        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            Ensure(await users.AccessFailedAsync(user));
            return new Rejected("Invalid credentials.");
        }
        if (await db.Employees.AnyAsync(value => value.UserId == user.Id && !value.IsActive, ct))
        {
            return new Rejected("Invalid credentials.");
        }
        if (user.TwoFactorEnabled)
        {
            return new Rejected("Two-factor login is not enabled in this release.");
        }
        Ensure(await users.ResetAccessFailedCountAsync(user));
        var device = tokens.Device(request.DeviceName);
        var session = Session.Create(user.Id, device, now, tokens.SessionExpiry(now));
        db.Add(session);
        return accounts.Issue(user, session, request.Mode);
    }
}