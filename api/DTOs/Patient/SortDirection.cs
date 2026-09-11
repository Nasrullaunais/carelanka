using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Ascending or descending. The group-owned SortDir parameter in all five specs, so the name is
/// not this component's to change — it moves to DTOs/Common the moment a second component pages.
/// </summary>
[TypeConverter(typeof(SnakeCaseEnumTypeConverter<SortDirection>))]
public enum SortDirection
{
    Asc,
    Desc
}
