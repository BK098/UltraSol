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

public sealed record RemovePermissionGrantCommand(Guid GrantId) : ICommand<ApiResult<object>>, ISystemAuthRequest;
public sealed class RemovePermissionGrantValidator : AbstractValidator<RemovePermissionGrantCommand>
{
    public RemovePermissionGrantValidator()
    {
        RuleFor(x => x.GrantId).NotEmpty();
    }
}
internal sealed class RemovePermissionGrantCommandHandler(IAuthRepository db, IAuthUnitOfWork unitOfWork) : ICommandHandler<RemovePermissionGrantCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RemovePermissionGrantCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await db.LockAsync(ct);
            var grant = await db.DirectPermissions.SingleOrDefaultAsync(value => value.Id == request.GrantId, ct) ?? throw new KeyNotFoundException();
            db.Remove(grant);
            return ApiResultBuilder.Success<object>(new { Removed = grant.Id });
        }, cancellationToken);
}