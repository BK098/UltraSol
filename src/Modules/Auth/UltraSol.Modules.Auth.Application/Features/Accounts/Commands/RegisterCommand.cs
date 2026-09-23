using UltraSol.Shared.Application.Authentication;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Modules.Auth.Domain.Authorization;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using static UltraSol.Modules.Auth.Application.Authentication.AccountSessions;

namespace UltraSol.Modules.Auth.Application.Features.Accounts.Commands;

public sealed record RegisterCommand(string Email, string Password) : ICommand<ApiResult<object>>, IAnonymousAuthRequest;
public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(4096);
    }
}
internal sealed class RegisterCommandHandler(IAuthRepository db, UserManager<UserAccount> users, IAuthUnitOfWork unitOfWork) : ICommandHandler<RegisterCommand, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var result = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await db.LockAsync(ct);
            return await ExecuteAsync(request, ct);
        }, cancellationToken);
        return ApiResultBuilder.Success<object>(result);
    }
    private async Task<object> ExecuteAsync(RegisterCommand request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var user = UserAccount.Create(request.Email, now);
        Ensure(await users.CreateAsync(user, request.Password));
        var role = await db.Roles.SingleAsync(value => value.Code == "Customer", ct);
        db.Add(new RoleAssignment(Guid.CreateVersion7(), user.Id, role.Id));
        return new { user.Id, user.Email };
    }
}