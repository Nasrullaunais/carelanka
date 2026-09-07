using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CareLanka.Api.Common.Persistence;

/// <summary>
/// Stores an enum as its <c>snake_case</c> name (ADR 5).
/// <para>
/// EF Core's own <c>HasConversion&lt;string&gt;()</c> would store <c>WardNurse</c>, which
/// is not what the specs publish — the column and the JSON value must be the same string.
/// </para>
/// </summary>
public sealed class SnakeCaseEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public SnakeCaseEnumConverter()
        : base(value => EnumWire.ToWire(value), stored => EnumWire.FromWire<TEnum>(stored))
    {
    }
}
