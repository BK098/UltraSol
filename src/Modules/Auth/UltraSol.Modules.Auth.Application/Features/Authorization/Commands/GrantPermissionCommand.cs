using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Auth.Domain.Authorization;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Auth.Application.Features.Authorization.Commands;

public sealed record GrantPermissionCommand(Guid UserId, string Permission, bool Deny) : ICommand<ApiResult<object>>, ISystemAuthRequest;
public sealed class GrantPermissionValidator : AbstractValidator<GrantPermissionCommand>
{
    public GrantPermissionValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Permission).NotEmpty().MaximumLength(256);
    }
}
internal sealed class GrantPermissionCommandHandler(IAuthRepository db, AuthorizationRules administration, IAuthUnitOfWork unitOfWork) : ICommandHandler<GrantPermissionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(GrantPermissionCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await db.LockAsync(ct);
            await administration.RequireUser(request.UserId, ct);
            await administration.RequirePermission(request.Permission, ct);
            var direct = new DirectPermission(Guid.CreateVersion7(), request.UserId, request.Permission, request.Deny);
            db.Add(direct);
            return ApiResultBuilder.Success<object>(direct);
        }, cancellationToken);
}