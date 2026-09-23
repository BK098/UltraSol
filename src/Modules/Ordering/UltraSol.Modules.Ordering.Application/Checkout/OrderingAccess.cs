using System.Security.Cryptography;
using System.Text;
using UltraSol.Shared.Application.Authentication;

namespace UltraSol.Modules.Ordering.Application.Checkout;

public sealed class OrderingAccess(ICurrentAccount current)
{
    public Guid? UserId => current.UserId;
    public string UserKey => current.UserId is { } id ? "u:" + id.ToString("N") : throw new OrderingFailure(401, "AuthenticationRequired", "Authentication required.");
    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public static bool Matches(string? hash, string? token)
    {
        return hash is { Length: 64 } && token is { Length: 64 } && token.All(char.IsAsciiHexDigit)
            && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash), Encoding.ASCII.GetBytes(Hash(token)));
    }
    public void Demand(string ownerKey, string? guestHash, string? token)
    {
        if (current.UserId is { } id && ownerKey == "u:" + id.ToString("N") || Matches(guestHash, token))
        {
            return;
        }
        throw new OrderingFailure(404, "NotFound", "Resource was not found.");
    }
}