using System.Diagnostics.CodeAnalysis;

namespace UltraSol.Shared.Domain.Common.Extensions
{
    public static class StringExtensions
    {
        public static bool HasValue([NotNullWhen(true)] this string? value) =>
            !string.IsNullOrWhiteSpace(value);
    }
}