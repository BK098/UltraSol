using UltraSol.Shared.Application.Authentication;
using UltraSol.Modules.Auth.Application.Authentication;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Auth.Application.Features.Accounts.Queries;

public sealed record MeQuery : IQuery<ApiResult<object>>, ISelfServiceAuthRequest;
internal sealed class MeQueryHandler(AccountSessions accounts, IPermissionService permissions) : IQueryHandler<MeQuery, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(MeQuery request, CancellationToken cancellationToken)
    {
        var result = await ReadAsync(request, cancellationToken);
        return ApiResultBuilder.Success<object>(result);
    }
    private async Task<object> ReadAsync(MeQuery request, CancellationToken ct)
    {

        var user = await accounts.CurrentAsync(ct);
        return new { user.Id, user.Email, user.Status, Permissions = await permissions.GetAsync(user.Id, ct) };
    }
}