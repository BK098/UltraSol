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

public sealed record DeleteRoleCommand(Guid RoleId) : ICommand<ApiResult<object>>, ISystemAuthRequest;
public sealed class DeleteRoleValidator : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
    }
}
internal sealed class DeleteRoleCommandHandler(IAuthRepository db, IAuthUnitOfWork unitOfWork) : ICommandHandler<DeleteRoleCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(DeleteRoleCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await db.LockAsync(ct);
            var role = await db.Roles.SingleOrDefaultAsync(value => value.Id == request.RoleId, ct) ?? throw new KeyNotFoundException();
            if (role.Code is "System" or "Customer")
            {
                throw new DomainException("Seed roles cannot be deleted.");
            }
            db.RemoveRange(await db.RoleAssignments.Where(value => value.RoleId == role.Id).ToListAsync(ct));
            db.Remove(role);
            return ApiResultBuilder.Success<object>(new { Deleted = role.Id });
        }, cancellationToken);
}