using System.Net;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;
using UltraSol.Shared.Domain.Common.ValueObjects;

namespace UltraSol.Modules.Auth.Domain.Sessions;

public sealed class Device : ValueObject
{
    private Device() { }

    public string Name { get; private set; } = null!;
    public string? UserAgent { get; private set; }
    public string? IpAddress { get; private set; }

    public static Device Create(string name, string? userAgent = null, string? ipAddress = null)
    {
        name = Guard.Required(name, "Device name");
        userAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim();
        ipAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim();
        if (name.Length > 128 || userAgent?.Length > 1024)
        {
            throw new DomainException("Device name or user agent exceeds its maximum length.");
        }
        if (ipAddress is not null)
        {
            if (!IPAddress.TryParse(ipAddress, out var address))
            {
                throw new DomainException("Device IP address is invalid.");
            }
            ipAddress = address.ToString();
        }
        return new Device { Name = name, UserAgent = userAgent, IpAddress = ipAddress };
    }

    protected override IEnumerable<object?> GetEqualityComponents() => [Name, UserAgent, IpAddress];
}