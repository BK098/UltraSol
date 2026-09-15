using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Auth.Application.Features.Authorization.Queries;

public sealed record RolesQuery : IQuery<ApiResult<object>>, ISystemAuthRequest;
internal sealed class RolesQueryHandler(IAuthRepository db) : IQueryHandler<RolesQuery, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(RolesQuery request, CancellationToken ct) =>
        ApiResultBuilder.Success<object>(new { Roles = await db.Roles.AsNoTracking().ToListAsync(ct), Permissions = await db.RolePermissions.AsNoTracking().ToListAsync(ct) });
}