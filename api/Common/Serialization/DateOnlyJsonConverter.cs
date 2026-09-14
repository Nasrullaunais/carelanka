using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CareLanka.Api.Common.Serialization;

/// <summary>
/// Reads a <see cref="DateOnly"/> from either <c>2026-04-02</c> or a full
/// ISO-8601 timestamp, and always writes the date-only form.
/// </summary>
/// <remarks>
/// Dart has no date-only type, so <c>swagger_parser</c> generates
/// <c>DateTime</c> for every <c>format: date</c> field and serialises it as
/// <c>2026-04-02T00:00:00.000</c>. The generated client cannot be hand-edited,
/// so the API accepts that form and discards the time. Without this, every
/// date the Flutter app sends is a 400.
/// </remarks>
public sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    private const string WireFormat = "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var value = reader.GetString() ?? string.Empty;

        // The leading ten characters, and only those. Parsing the whole
        // timestamp instead would convert the offset and land on the day
        // before for anything late in the evening - and a general
        // DateTime.Parse would read "02/04/1995" as 4 February without
        // complaining, which is a wrong date rather than a rejected one.
        var datePart = value.Length >= WireFormat.Length ? value[..WireFormat.Length] : value;
        var remainder = value[datePart.Length..];

        if ((remainder.Length == 0 || remainder[0] == 'T')
            && DateOnly.TryParseExact(datePart, WireFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            return date;
        }

        throw new JsonException($"Expected a date like {WireFormat}, got '{value}'.");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(WireFormat, CultureInfo.InvariantCulture));
}
