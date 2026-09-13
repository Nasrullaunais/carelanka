using System.ComponentModel;
using System.Globalization;

namespace CareLanka.Api.Common.Persistence;

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
