using FluentValidation;
using Microsoft.AspNetCore.Identity;
using UltraSol.Modules.Auth.Application.Authentication;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using static UltraSol.Modules.Auth.Application.Authentication.AccountSessions;

namespace UltraSol.Modules.Auth.Application.Features.Accounts.Commands;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword, bool RevokeOtherSessions = true) : ICommand<ApiResult<object>>, ISelfServiceAuthRequest;
public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.NewPassword).NotEmpty().MaximumLength(4096);
    }
}
internal sealed class ChangePasswordCommandHandler(IAuthRepository db, UserManager<UserAccount> users, AccountSessions accounts, IAuthUnitOfWork unitOfWork) : ICommandHandler<ChangePasswordCommand, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var result = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await db.LockAsync(ct);
            return await ExecuteAsync(request, ct);
        }, cancellationToken);
        return ApiResultBuilder.Success(result);
    }
    private async Task<object> ExecuteAsync(ChangePasswordCommand request, CancellationToken ct)
    {

        var user = await accounts.CurrentAsync(ct);
        Ensure(await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword));
        if (request.RevokeOtherSessions)
        {
            await accounts.RevokeAsync(user.Id, null, true, "Password changed", ct);
        }
        return new { Changed = true };
    }
}