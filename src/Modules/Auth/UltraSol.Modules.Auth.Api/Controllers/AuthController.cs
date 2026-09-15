using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Auth.Api.Controllers;

[Route("api/auth")]
internal sealed class AuthController(ISender sender) : BaseController(sender)
{
    [HttpGet("csrf")]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery) => Ok(new { Token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    [HttpPost("register")]
    public Task<IActionResult> Register(RegisterCommand command, CancellationToken ct) => SendAsync(command, ct);
    [HttpPost("login")]
    public Task<IActionResult> Login(LoginCommand command, CancellationToken ct) => SendAsync(command with { Mode = "cookie" }, ct);
    [HttpPost("token")]
    public Task<IActionResult> Token(LoginCommand command, CancellationToken ct) => SendAsync(command with { Mode = "token" }, ct);
    [HttpGet("me")]
    public Task<IActionResult> Me(CancellationToken ct) => SendAsync(new MeQuery(), ct);
    [HttpGet("sessions")]
    public Task<IActionResult> Sessions(CancellationToken ct) => SendAsync(new SessionsQuery(), ct);
    [HttpPost("logout")]
    public Task<IActionResult> Logout(CancellationToken ct) => SendAsync(new LogoutCommand(), ct);
    [HttpPost("sessions/revoke")]
    public Task<IActionResult> Revoke(RevokeSessionsCommand command, CancellationToken ct) => SendAsync(command, ct);
    [HttpPost("password")]
    public Task<IActionResult> Password(ChangePasswordCommand command, CancellationToken ct) => SendAsync(command, ct);
    [HttpGet("accounts")]
    public Task<IActionResult> Accounts(CancellationToken ct) => SendAsync(new AccountsQuery(), ct);
    [HttpPut("accounts/status")]
    public Task<IActionResult> Status(ChangeAccountStatusCommand command, CancellationToken ct) => SendAsync(command, ct);
}