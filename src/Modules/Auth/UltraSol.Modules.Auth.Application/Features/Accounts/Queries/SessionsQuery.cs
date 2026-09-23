using UltraSol.Shared.Application.Authentication;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Auth.Application.Features.Accounts.Queries;

public sealed record SessionsQuery : IQuery<ApiResult<object>>, ISelfServiceAuthRequest;
internal sealed class SessionsQueryHandler(IAuthRepository db, ICurrentAccount current) : IQueryHandler<SessionsQuery, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(SessionsQuery request, CancellationToken cancellationToken)
    {
        var result = await ReadAsync(request, cancellationToken);
        return ApiResultBuilder.Success<object>(result);
    }
    private async Task<object> ReadAsync(SessionsQuery request, CancellationToken ct)
    {

        return await db.Sessions.AsNoTracking().Where(value => value.UserId == current.UserId).OrderByDescending(value => value.CreatedAt).Select(value => new { value.Id, Device = value.Device.Name, value.CreatedAt, value.LastSeenAt, value.ExpiresAt, value.RevokedAt, value.RevokeReason }).Take(200).ToListAsync(ct);
    }
}