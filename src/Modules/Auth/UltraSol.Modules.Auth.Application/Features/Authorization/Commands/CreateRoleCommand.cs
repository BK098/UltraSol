using FluentValidation;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Auth.Domain.Authorization;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Auth.Application.Features.Authorization.Commands;

public sealed record CreateRoleCommand(string Code, string Name) : ICommand<ApiResult<object>>, ISystemAuthRequest;
public sealed class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
    }
}
internal sealed class CreateRoleCommandHandler(IAuthRepository db, IAuthUnitOfWork unitOfWork) : ICommandHandler<CreateRoleCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(CreateRoleCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await db.LockAsync(ct);
            if (string.Equals(request.Code, "System", StringComparison.OrdinalIgnoreCase) || string.Equals(request.Code, "Customer", StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException("Reserved role code.");
            }
            var role = new Role(request.Code, request.Name);
            db.Add(role);
            return ApiResultBuilder.Success<object>(role);
        }, cancellationToken);
}