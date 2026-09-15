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

public sealed record RemoveRoleAssignmentCommand(Guid AssignmentId) : ICommand<ApiResult<object>>, ISystemAuthRequest;
public sealed class RemoveRoleAssignmentValidator : AbstractValidator<RemoveRoleAssignmentCommand>
{
    public RemoveRoleAssignmentValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
    }
}
internal sealed class RemoveRoleAssignmentCommandHandler(IAuthRepository db, AuthorizationRules administration, IAuthUnitOfWork unitOfWork) : ICommandHandler<RemoveRoleAssignmentCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RemoveRoleAssignmentCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await db.LockAsync(ct);
            var assignment = await db.RoleAssignments.SingleOrDefaultAsync(value => value.Id == request.AssignmentId, ct) ?? throw new KeyNotFoundException();
            await administration.ProtectLastSystemAsync(assignment.UserId, assignment.Id, ct);
            db.Remove(assignment);
            return ApiResultBuilder.Success<object>(new { Removed = assignment.Id });
        }, cancellationToken);
}