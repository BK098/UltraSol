using UltraSol.Modules.Auth.Application.Authentication;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Auth.Application.Features.Accounts.Commands;

public sealed record RevokeSessionsCommand(Guid? SessionId = null, bool KeepCurrent = false) : ICommand<ApiResult<object>>, ISelfServiceAuthRequest;
internal sealed class RevokeSessionsCommandHandler(IAuthRepository db, ICurrentAccount current, AccountSessions accounts, IAuthUnitOfWork unitOfWork, IAuthTokens tokens) : ICommandHandler<RevokeSessionsCommand, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(RevokeSessionsCommand request, CancellationToken cancellationToken)
    {
        var result = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await db.LockAsync(ct);
            return await ExecuteAsync(request, ct);
        }, cancellationToken);
        if (!request.KeepCurrent && (request.SessionId is null || request.SessionId == current.SessionId))
        {
            await tokens.SignOutAsync();
        }
        return ApiResultBuilder.Success<object>(result);
    }
    private async Task<object> ExecuteAsync(RevokeSessionsCommand request, CancellationToken ct)
    {
        await accounts.RevokeAsync(current.UserId!.Value, request.SessionId, request.KeepCurrent, "User revoked session", ct);
                return new { Revoked = true };
    }
}