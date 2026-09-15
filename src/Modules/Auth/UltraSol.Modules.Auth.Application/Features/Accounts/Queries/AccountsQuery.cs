using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Auth.Application.Features.Accounts.Queries;

public sealed record AccountsQuery : IQuery<ApiResult<object>>, ISystemAuthRequest;
internal sealed class AccountsQueryHandler(IAuthRepository db) : IQueryHandler<AccountsQuery, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(AccountsQuery request, CancellationToken cancellationToken)
    {
        var result = await ReadAsync(request, cancellationToken);
        return ApiResultBuilder.Success<object>(result);
    }
    private async Task<object> ReadAsync(AccountsQuery request, CancellationToken ct)
    {
        return await db.Users.AsNoTracking().OrderBy(value => value.Email).Select(value => new { value.Id, value.Email, value.Status, value.CreatedAt }).Take(200).ToListAsync(ct);
    }
}