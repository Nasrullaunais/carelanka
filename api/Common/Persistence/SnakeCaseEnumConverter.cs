using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CareLanka.Api.Common.Persistence;

// EF Core's own HasConversion<string>() would store "WardNurse"; the specs publish "ward_nurse".
public sealed class SnakeCaseEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public SnakeCaseEnumConverter()
        : base(value => EnumWire.ToWire(value), stored => EnumWire.FromWire<TEnum>(stored))
    {
    }
}
