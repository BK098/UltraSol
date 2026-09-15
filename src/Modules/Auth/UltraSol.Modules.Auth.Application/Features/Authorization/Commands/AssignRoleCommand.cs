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

public sealed record AssignRoleCommand(Guid UserId, Guid RoleId) : ICommand<ApiResult<object>>, ISystemAuthRequest;
public sealed class AssignRoleValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RoleId).NotEmpty();
    }
}
internal sealed class AssignRoleCommandHandler(IAuthRepository db, AuthorizationRules administration, IAuthUnitOfWork unitOfWork) : ICommandHandler<AssignRoleCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(AssignRoleCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await db.LockAsync(ct);
            await administration.RequireUser(request.UserId, ct);
            var role = await db.Roles.SingleOrDefaultAsync(value => value.Id == request.RoleId, ct) ?? throw new KeyNotFoundException();
            if (role.Code == "Customer")
            {
                if (await db.Employees.AnyAsync(value => value.UserId == request.UserId, ct))
                {
                    throw new DomainException("An employee cannot also be Customer.");
                }
            }
            var assignment = new RoleAssignment(Guid.CreateVersion7(), request.UserId, request.RoleId);
            db.Add(assignment);
            return ApiResultBuilder.Success<object>(assignment);
        }, cancellationToken);
}