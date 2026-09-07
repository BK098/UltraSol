using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using UltraSol.Shared.Domain.Common.Delegates;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Shared.Domain.Common.Guards;

public static class Guard
{
    public static string Required(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{field} is required.");
        }
        return value.Trim();
    }

    public static Guid Id(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("An ID cannot be empty.");
        }
        return value;
    }

    public static string MediaUrl(string? value)
    {
        var url = Required(value, "Media URL");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(uri.Host))
            throw new DomainException("Media URL must be an absolute HTTP or HTTPS URL.");
        return url;
    }
    public static T AgainstNull<T>(
        [NotNull] T? value,
        [CallerArgumentExpression(nameof(value))] string? name = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, name);
        return value;
    }

    public static string AgainstNullOrWhiteSpace(
        [NotNull] string? value,
        [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value;
    }

    public static T AgainstDefault<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? name = null)
        where T : struct
    {
        if (EqualityComparer<T>.Default.Equals(value, default))
        {
            throw new ArgumentException($"{name} must not be the default value.", name);
        }

        return value;
    }

    public static T Against<T>(
        T value,
        BusinessRule<T> rule,
        string message,
        [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (rule(value))
        {
            throw new ArgumentException(message, name);
        }

        return value;
    }

    public static T Ensure<T>(
        T value,
        BusinessRule<T> rule,
        ExceptionFactory exceptionFactory,
        [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (!rule(value))
        {
            throw exceptionFactory(name ?? nameof(value));
        }

        return value;
    }

    public static void AgainstOutOfRange(
        int value,
        int min,
        int max,
        [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(name, value, $"Value must be between {min} and {max}.");
        }
    }

    public static T NotFound<T>(
        T? entity,
        object? id,
        [CallerArgumentExpression(nameof(entity))] string? name = null)
        where T : class
    {
        return entity ?? throw EntityNotFoundException.For<T>(id);
    }
}