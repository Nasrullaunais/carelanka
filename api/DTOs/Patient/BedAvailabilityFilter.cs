using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.DTOs.Patient;

[TypeConverter(typeof(SnakeCaseEnumTypeConverter<BedAvailabilityFilter>))]
public enum BedAvailabilityFilter
{
    Free,
    Reserved,
    Occupied,
    OutOfService,
    All
}
