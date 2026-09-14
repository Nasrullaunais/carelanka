using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.DTOs.Patient;

[TypeConverter(typeof(SnakeCaseEnumTypeConverter<SortDirection>))]
public enum SortDirection
{
    Asc,
    Desc
}
