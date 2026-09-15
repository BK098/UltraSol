using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Auth.Application.Authentication;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using static UltraSol.Modules.Auth.Application.Authentication.AccountSessions;

namespace UltraSol.Modules.Auth.Application.Features.Accounts.Commands;
//Todo: Tách ra 3 hàm khách nhau để phân biệt đang status nào không gộp chung
public sealed record ChangeAccountStatusCommand(Guid UserId, string Status) : ICommand<ApiResult<object>>, ISystemAuthRequest;
public sealed class ChangeAccountStatusValidator : AbstractValidator<ChangeAccountStatusCommand>
{
    public ChangeAccountStatusValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Status).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Status).Must(status => status is "Active" or "Suspended" or "Deleted");
    }
}
internal sealed class ChangeAccountStatusCommandHandler(IAuthRepository db, UserManager<UserAccount> users, AccountSessions accounts, AuthorizationRules administration, IAuthUnitOfWork unitOfWork) : ICommandHandler<ChangeAccountStatusCommand, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(ChangeAccountStatusCommand request, CancellationToken cancellationToken)
    {
        var result = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await db.LockAsync(ct);
            return await ExecuteAsync(request, ct);
        }, cancellationToken);
        return ApiResultBuilder.Success<object>(result);
    }
    private async Task<object> ExecuteAsync(ChangeAccountStatusCommand request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var user = await db.Users.SingleOrDefaultAsync(value => value.Id == request.UserId, ct) ?? throw new KeyNotFoundException();
        if (request.Status != "Active")
        {
            await administration.ProtectLastSystemAsync(user.Id, null, ct);
        }
        switch (request.Status)
        {
            case "Active": user.Activate(now); break;
            case "Suspended": user.Suspend(now); break;
            case "Deleted": user.SoftDelete(now); break;
            default: throw new DomainException("Invalid account status.");
        }
        if (request.Status != "Active")
        {
            await accounts.RevokeAsync(user.Id, null, false, "Account status changed", ct);
        }
        Ensure(await users.UpdateAsync(user));
        return new { user.Id, user.Status };
    }
}