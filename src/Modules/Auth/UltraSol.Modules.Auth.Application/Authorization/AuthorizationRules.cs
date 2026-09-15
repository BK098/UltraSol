using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Auth.Application.Authorization;

public sealed class AuthorizationRules(IAuthRepository db)
{

    public async Task ProtectLastSystemAsync(Guid userId, Guid? assignmentId, CancellationToken ct)
    {
        var assignments = from assignment in db.RoleAssignments
                          join role in db.Roles on assignment.RoleId equals role.Id
                          join user in db.Users on assignment.UserId equals user.Id
                          where role.Code == "System" && user.Status == AccountStatus.Active && !db.Employees.Any(employee => employee.UserId == user.Id && !employee.IsActive)
                          select assignment;
        if (!await assignments.AnyAsync(value => value.UserId == userId && (!assignmentId.HasValue || value.Id == assignmentId), ct))
        {
            return;
        }
        if (!await assignments.AnyAsync(value => assignmentId.HasValue ? value.Id != assignmentId : value.UserId != userId, ct))
        {
            throw new DomainException("At least one active System account must remain.");
        }
    }

    public async Task RequireUser(Guid id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(value => value.Id == id, ct))
        {
            throw new KeyNotFoundException();
        }
    }

    public async Task RequirePermission(string code, CancellationToken ct)
    {
        if (!await db.Permissions.AnyAsync(value => value.Code == code && value.IsActive, ct))
        {
            throw new DomainException("Unknown or inactive permission.");
        }
    }
}