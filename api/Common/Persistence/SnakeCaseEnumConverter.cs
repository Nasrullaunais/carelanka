using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CareLanka.Api.Common.Persistence;

public sealed class SnakeCaseEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public SnakeCaseEnumConverter()
        : base(value => EnumWire.ToWire(value), stored => EnumWire.FromWire<TEnum>(stored))
    {
    }
}
