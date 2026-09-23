using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UltraSol.Modules.Ordering.Infrastructure.Persistence;

internal static class OrderingJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    internal static void Configure<T>(PropertyBuilder<T> property)
    {
        property.HasColumnType("jsonb").HasConversion(value => Serialize(value), value => Deserialize<T>(value));
        property.Metadata.SetValueComparer(new ValueComparer<T>(
            (left, right) => Serialize(left) == Serialize(right),
            value => Serialize(value).GetHashCode(StringComparison.Ordinal),
            value => Deserialize<T>(Serialize(value))));
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    private static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)!;
}