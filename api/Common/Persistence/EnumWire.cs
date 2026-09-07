using System.Collections.Concurrent;
using System.Text;

namespace CareLanka.Api.Common.Persistence;

/// <summary>
/// One vocabulary for enums, in the database and on the wire.
/// <para>
/// ADR 5: enums are stored as <c>snake_case</c> strings with a CHECK constraint, and the
/// specs publish those same strings as JSON. <c>SELECT * FROM staff_members</c> is readable
/// during the demo, and there is one set of values to learn rather than two.
/// </para>
/// </summary>
public static class EnumWire
{
    private static readonly ConcurrentDictionary<Type, Dictionary<string, object>> FromWireMaps = new();

    /// <summary>The stored / published form: <c>WardNurse</c> becomes <c>ward_nurse</c>.</summary>
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

    /// <summary>Every value of the enum, in stored form — used to build CHECK constraints.</summary>
    public static IReadOnlyList<string> Values<TEnum>() where TEnum : struct, Enum
        => Enum.GetValues<TEnum>().Select(v => ToSnakeCase(v.ToString())).ToList();

    /// <summary>
    /// <c>role IN ('ward_nurse', 'doctor', ...)</c> — the SQL half of ADR 5, written in the
    /// same configuration class as the property so the two cannot drift out of sight of
    /// each other.
    /// </summary>
    public static string CheckConstraint<TEnum>(string columnName) where TEnum : struct, Enum
        => $"{columnName} IN ({string.Join(", ", Values<TEnum>().Select(v => $"'{v}'"))})";

    private static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];

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
