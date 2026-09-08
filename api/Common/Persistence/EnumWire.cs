using System.Collections.Concurrent;
using System.Text;

namespace CareLanka.Api.Common.Persistence;

public static class EnumWire
{
    private static readonly ConcurrentDictionary<Type, Dictionary<string, object>> FromWireMaps = new();

    public static string ToWire<TEnum>(TEnum value) where TEnum : struct, Enum
        => ToSnakeCase(value.ToString());

    public static TEnum FromWire<TEnum>(string wire) where TEnum : struct, Enum
    {
        var map = FromWireMaps.GetOrAdd(typeof(TEnum), static _ =>
            Enum.GetValues<TEnum>().ToDictionary(v => ToSnakeCase(v.ToString()), v => (object)v));

        return map.TryGetValue(wire, out var value)
            ? (TEnum)value
            : throw new ArgumentOutOfRangeException(
                nameof(wire), wire, $"'{wire}' is not a {typeof(TEnum).Name} value.");
    }

    public static IReadOnlyList<string> Values<TEnum>() where TEnum : struct, Enum
        => Enum.GetValues<TEnum>().Select(v => ToSnakeCase(v.ToString())).ToList();

    public static string CheckConstraint<TEnum>(string columnName) where TEnum : struct, Enum
        => $"{columnName} IN ({string.Join(", ", Values<TEnum>().Select(v => $"'{v}'"))})";

    private static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];

            // Also breaks before the last letter of an uppercase run: HTTPServer -> http_server.
            if (char.IsUpper(c) && i > 0 && (!char.IsUpper(name[i - 1]) ||
                    (i + 1 < name.Length && char.IsLower(name[i + 1]))))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
