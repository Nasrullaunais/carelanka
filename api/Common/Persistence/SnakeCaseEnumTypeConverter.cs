using System.ComponentModel;
using System.Globalization;

namespace CareLanka.Api.Common.Persistence;

/// <summary>
/// Lets an enum be used as a query-string parameter on its published wire value.
/// </summary>
/// <remarks>
/// The JSON converter only covers request and response bodies. Query strings go through MVC's
/// model binder, which matches the C# member name — so <c>?sortBy=created_at</c> fails to bind
/// to <c>CreatedAt</c> and the request 400s for a value the spec says is valid. Single-word
/// values happen to work by accident, which is why nothing has hit this until now.
///
/// Added by Patient Management (M4) for <c>GET /api/patients?sortBy=</c>, the first query
/// parameter in the API whose enum has a multi-word value. It is not component-specific:
/// put <c>[TypeConverter(typeof(SnakeCaseEnumTypeConverter&lt;YourEnum&gt;))]</c> on any enum
/// that appears in a query string.
/// </remarks>
public sealed class SnakeCaseEnumTypeConverter<TEnum> : TypeConverter
    where TEnum : struct, Enum
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(
        ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string wire)
        {
            return base.ConvertFrom(context, culture, value);
        }

        // Throwing is the point: MVC turns it into a 400 naming the parameter, where returning a
        // default would silently sort by something the caller did not ask for.
        return EnumWire.FromWire<TEnum>(wire.Trim());
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(
        ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => destinationType == typeof(string) && value is TEnum enumValue
            ? EnumWire.ToWire(enumValue)
            : base.ConvertTo(context, culture, value, destinationType);
}
