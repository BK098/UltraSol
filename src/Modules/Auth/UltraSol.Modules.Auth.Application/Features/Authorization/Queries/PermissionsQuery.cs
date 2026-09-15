using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Auth.Application.Features.Authorization.Queries;

public sealed record PermissionsQuery : IQuery<ApiResult<object>>, ISystemAuthRequest;
internal sealed class PermissionsQueryHandler(IAuthRepository db) : IQueryHandler<PermissionsQuery, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(PermissionsQuery request, CancellationToken ct) =>
        ApiResultBuilder.Success<object>(await db.Permissions.AsNoTracking().Where(value => value.IsActive).OrderBy(value => value.Code).ToListAsync(ct));
}