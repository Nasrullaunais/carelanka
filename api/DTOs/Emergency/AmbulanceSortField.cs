using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.DTOs.Emergency;

[TypeConverter(typeof(SnakeCaseEnumTypeConverter<AmbulanceSortField>))]
public enum AmbulanceSortField
{
    RegistrationNumber,
    Status,
    Distance
}
