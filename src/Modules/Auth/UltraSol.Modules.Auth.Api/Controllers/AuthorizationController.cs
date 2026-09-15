using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Auth.Api.Controllers;

[Route("api/authorization")]
internal sealed class AuthorizationController(ISender sender) : BaseController(sender)
{
    [HttpGet("permissions")]
    public Task<IActionResult> Permissions(CancellationToken ct) => SendAsync(new PermissionsQuery(), ct);
    [HttpGet("roles")]
    public Task<IActionResult> Roles(CancellationToken ct) => SendAsync(new RolesQuery(), ct);
    [HttpGet("users/{userId:guid}/grants")]
    public Task<IActionResult> Grants(Guid userId, CancellationToken ct) => SendAsync(new UserGrantsQuery(userId), ct);
    [HttpPost("roles")]
    public Task<IActionResult> CreateRole(CreateRoleCommand command, CancellationToken ct) => SendAsync(command, ct);
    [HttpPut("roles")]
    public Task<IActionResult> RenameRole(RenameRoleCommand command, CancellationToken ct) => SendAsync(command, ct);
    [HttpPost("roles/delete")]
    public Task<IActionResult> DeleteRole(DeleteRoleCommand command, CancellationToken ct) => SendAsync(command, ct);
    [HttpPut("roles/permissions")]
    public Task<IActionResult> RolePermission(SetRolePermissionCommand command, CancellationToken ct) => SendAsync(command, ct);
    [HttpPost("assignments")]
    public Task<IActionResult> Assign(AssignRoleCommand command, CancellationToken ct) => SendAsync(command, ct);
    [HttpPost("assignments/remove")]
    public Task<IActionResult> RemoveAssignment(RemoveRoleAssignmentCommand command, CancellationToken ct) => SendAsync(command, ct);
    [HttpPost("grants")]
    public Task<IActionResult> Grant(GrantPermissionCommand command, CancellationToken ct) => SendAsync(command, ct);
    [HttpPost("grants/remove")]
    public Task<IActionResult> RemoveGrant(RemovePermissionGrantCommand command, CancellationToken ct) => SendAsync(command, ct);
}