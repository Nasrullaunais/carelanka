using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.DTOs.Emergency;

[TypeConverter(typeof(SnakeCaseEnumTypeConverter<EmergencyCallSortField>))]
public enum EmergencyCallSortField
{
    Priority,
    CreatedAt,
    Status
}
