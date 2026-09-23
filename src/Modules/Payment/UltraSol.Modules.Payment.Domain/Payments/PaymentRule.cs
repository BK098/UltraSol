using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Payment.Domain.Payments;

internal static class PaymentRule
{
    internal static void Require(bool condition, string message, string code = "InvalidPayment")
    {
        if (!condition)
        {
            throw new DomainException(message, code);
        }
    }

    internal static string Text(string? value, int maximum, string name)
    {
        Require(!string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximum, $"{name} is required and must be at most {maximum} characters.");
        return value!.Trim();
    }

    internal static decimal Amount(decimal amount, bool allowZero = false)
    {
        Require(amount >= 0 && (allowZero || amount > 0) && decimal.Round(amount, 2) == amount, "Amount must be nonnegative with at most two decimal places.");
        return amount;
    }

    internal static string Currency(string value)
    {
        var currency = Text(value, 3, "Currency").ToUpperInvariant();
        Require(currency.Length == 3 && currency.All(char.IsAsciiLetter), "Currency must be three ASCII letters.");
        return currency;
    }
}
