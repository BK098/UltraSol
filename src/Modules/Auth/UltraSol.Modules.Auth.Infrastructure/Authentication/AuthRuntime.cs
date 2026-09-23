using UltraSol.Shared.Application.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UltraSol.Modules.Auth.Application.Authentication;
using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Modules.Auth.Domain.Sessions;
using UltraSol.Modules.Auth.Infrastructure.Persistence;

namespace UltraSol.Modules.Auth.Infrastructure.Authentication;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public string Issuer { get; set; } = "UltraSol";
    public string Audience { get; set; } = "UltraSol";
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int SessionDays { get; set; } = 7;
}

public sealed class CurrentAccount(IHttpContextAccessor accessor) : ICurrentAccount
{
    public Guid? UserId => Read(ClaimTypes.NameIdentifier);
    public Guid? SessionId => Read("sid");
    private Guid? Read(string type) => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(type), out var id) ? id : null;
}

public sealed class SessionAuthentication(AuthDbContext db)
{
    public async Task<bool> ValidateAsync(ClaimsPrincipal? principal, CancellationToken ct)
    {
        if (!Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !Guid.TryParse(principal?.FindFirstValue("sid"), out var sessionId))
        {
            return false;
        }
        var now = DateTimeOffset.UtcNow;
        var valid = await (from session in db.Sessions.AsNoTracking()
                           join user in db.Users.AsNoTracking() on session.UserId equals user.Id
                           where session.Id == sessionId && user.Id == userId && user.Status == AccountStatus.Active &&
                               session.RevokedAt == null && session.CreatedAt <= now && session.ExpiresAt > now
                           select session.Id).AnyAsync(ct);
        return valid && !await db.Set<EmployeeAccount>().AnyAsync(value => value.UserId == userId && !value.IsActive, ct);
    }
}

internal sealed class AuthTokens(IOptions<AuthOptions> configured, IHttpContextAccessor http) : IAuthTokens
{
    internal const string CookieScheme = "AuthCookie";
    private readonly AuthOptions _options = configured.Value;
    public DateTimeOffset SessionExpiry(DateTimeOffset now) => now.AddDays(_options.SessionDays);
    internal static ClaimsPrincipal Principal(Guid userId, Guid sessionId, string email) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim("sid", sessionId.ToString()), new Claim(ClaimTypes.Name, email)], CookieScheme));

    public string Access(Guid userId, Guid sessionId, string email)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            _options.Issuer, 
            _options.Audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()), 
                new Claim("sid", sessionId.ToString()), 
                //new Claim(JwtRegisteredClaimNames.Email, email)
            ],now, now.AddMinutes(_options.AccessTokenMinutes), 
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    public Device Device(string name) => UltraSol.Modules.Auth.Domain.Sessions.Device.Create(name, http.HttpContext?.Request.Headers.UserAgent.ToString(), http.HttpContext?.Connection.RemoteIpAddress?.ToString());
    public Task SignInAsync(LoginResult login) => http.HttpContext!.SignInAsync(CookieScheme, Principal(login.UserId, login.SessionId, login.Email),
        new AuthenticationProperties { IsPersistent = true, ExpiresUtc = login.ExpiresAt, AllowRefresh = false });
    public Task SignOutAsync() => http.HttpContext!.SignOutAsync(CookieScheme);
}