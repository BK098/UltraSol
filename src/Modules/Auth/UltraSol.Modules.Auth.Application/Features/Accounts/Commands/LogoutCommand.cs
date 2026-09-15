using UltraSol.Modules.Auth.Application.Authentication;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Auth.Application.Features.Accounts.Commands;

public sealed record LogoutCommand : ICommand<ApiResult<object>>, ISelfServiceAuthRequest;
internal sealed class LogoutCommandHandler(IAuthRepository db, ICurrentAccount current, AccountSessions accounts, IAuthUnitOfWork unitOfWork, IAuthTokens tokens) : ICommandHandler<LogoutCommand, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var result = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await db.LockAsync(ct);
            return await ExecuteAsync(ct);
        }, cancellationToken);
        await tokens.SignOutAsync();
        return ApiResultBuilder.Success<object>(result);
    }
    private async Task<object> ExecuteAsync( CancellationToken ct)
    {

        await accounts.RevokeAsync(current.UserId!.Value, current.SessionId, false, "Logout", ct);
        return true;
    }
}