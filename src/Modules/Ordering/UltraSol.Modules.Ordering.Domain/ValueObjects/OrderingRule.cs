using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Ordering.Domain.ValueObjects;

public static class OrderingRule
{
    public static void Require(bool condition, string message, string code = "InvalidOrder")
    {
        if (!condition)
        {
            throw new DomainException(message, code);
        }
    }

    public static string Text(string? value, int maximum, string name)
    {
        Require(!string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximum, $"{name} is required and must be at most {maximum} characters.");
        return value!.Trim();
    }

    public static string Currency(string value)
    {
        var normalized = Text(value, 3, "Currency").ToUpperInvariant();
        Require(normalized.Length == 3 && normalized.All(char.IsAsciiLetter), "Currency must be three ASCII letters.");
        return normalized;
    }
}