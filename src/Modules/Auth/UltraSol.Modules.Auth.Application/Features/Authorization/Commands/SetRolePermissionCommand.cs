using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Auth.Domain.Authorization;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Auth.Application.Features.Authorization.Commands;

public sealed record SetRolePermissionCommand(Guid RoleId, string Permission, bool Deny, bool Remove = false) : ICommand<ApiResult<object>>, ISystemAuthRequest;
public sealed class SetRolePermissionValidator : AbstractValidator<SetRolePermissionCommand>
{
    public SetRolePermissionValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.Permission).NotEmpty().MaximumLength(256);
    }
}
internal sealed class SetRolePermissionCommandHandler(IAuthRepository db, AuthorizationRules administration, IAuthUnitOfWork unitOfWork) : ICommandHandler<SetRolePermissionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(SetRolePermissionCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await db.LockAsync(ct);
            var role = await db.Roles.SingleOrDefaultAsync(value => value.Id == request.RoleId, ct) ?? throw new KeyNotFoundException();
            if (role.Code == "System")
            {
                throw new DomainException("System access is intrinsic, not a permission list.");
            }
            await administration.RequirePermission(request.Permission, ct);
            var previous = await db.RolePermissions.SingleOrDefaultAsync(value => value.RoleId == request.RoleId && value.PermissionCode == request.Permission, ct);
            if (previous is not null)
            {
                db.Remove(previous);
                await db.SaveAsync(ct);
            }
            if (!request.Remove)
            {
                db.Add(new RolePermission(request.RoleId, request.Permission, request.Deny));
            }
            return ApiResultBuilder.Success<object>(new { Updated = true });
        }, cancellationToken);
}