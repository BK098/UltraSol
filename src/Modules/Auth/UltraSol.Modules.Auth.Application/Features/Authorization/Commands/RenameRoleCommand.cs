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

public sealed record RenameRoleCommand(Guid RoleId, string Name) : ICommand<ApiResult<object>>, ISystemAuthRequest;
public sealed class RenameRoleValidator : AbstractValidator<RenameRoleCommand>
{
    public RenameRoleValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
    }
}
internal sealed class RenameRoleCommandHandler(IAuthRepository db, IAuthUnitOfWork unitOfWork) : ICommandHandler<RenameRoleCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RenameRoleCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await db.LockAsync(ct);
            var role = await db.Roles.SingleOrDefaultAsync(value => value.Id == request.RoleId, ct) ?? throw new KeyNotFoundException();
            role.Rename(request.Name);
            return ApiResultBuilder.Success<object>(role);
        }, cancellationToken);
}