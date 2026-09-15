using UltraSol.Modules.Auth.Domain.Sessions;

namespace UltraSol.Modules.Auth.Application.Authentication;

public sealed record Rejected(string Message);
public sealed record LoginResult(Guid UserId, Guid SessionId, string Email, string Mode, string? AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt);
public interface IAuthTokens
{
    DateTimeOffset SessionExpiry(DateTimeOffset now);
    string Access(Guid userId, Guid sessionId, string email);
    Device Device(string name);
    Task SignInAsync(LoginResult login);
    Task SignOutAsync();
}