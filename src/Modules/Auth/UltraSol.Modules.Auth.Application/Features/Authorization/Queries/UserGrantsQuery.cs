using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Auth.Application.Features.Authorization.Queries;

public sealed record UserGrantsQuery(Guid UserId) : IQuery<ApiResult<object>>, ISystemAuthRequest;
public sealed class UserGrantsValidator : AbstractValidator<UserGrantsQuery>
{
    public UserGrantsValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
internal sealed class UserGrantsQueryHandler(IAuthRepository db) : IQueryHandler<UserGrantsQuery, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(UserGrantsQuery request, CancellationToken ct) =>
        ApiResultBuilder.Success<object>(new { Roles = await db.RoleAssignments.AsNoTracking().Where(value => value.UserId == request.UserId).ToListAsync(ct), Direct = await db.DirectPermissions.AsNoTracking().Where(value => value.UserId == request.UserId).ToListAsync(ct) });
}